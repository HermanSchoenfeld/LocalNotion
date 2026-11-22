// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Sphere10.Framework.Web.VisualRenderer;
using Notion.Client;
using Sphere10.Framework;
using R = Sphere10.Framework.Web.VisualRenderer;

namespace LocalNotion.Core;

public sealed partial class NotionRenderModelBuilder {

	private VisualInline[] MapRichText(ProjectionContext context, IEnumerable<RichTextBase> text) {
		return (text ?? []).Where(item => item is not null).Select(Map).ToArray();

		VisualInline Map(RichTextBase item) {
			switch (item) {
				case RichTextEquation equation:
					return new EquationInline { Expression = equation.Equation?.Expression ?? string.Empty };
				case RichTextMention mention:
					return MapMention(context, mention);
				case RichTextText run:
					var content = run.Text?.Content ?? run.PlainText ?? string.Empty;
					var rawUrl = run.Text?.Link?.Url;
					return new TextInline {
						Text = content,
						Url = ResolveRichTextUrl(context, rawUrl),
						PlainTextOverride = !string.IsNullOrWhiteSpace(rawUrl) ? rawUrl : null,
						Style = new TextStyle {
							Bold = run.Annotations?.IsBold ?? false,
							Italic = run.Annotations?.IsItalic ?? false,
							Underline = run.Annotations?.IsUnderline ?? false,
							StrikeThrough = run.Annotations?.IsStrikeThrough ?? false,
							Code = run.Annotations?.IsCode ?? false,
							Color = MapColor(run.Annotations?.Color)
						}
					};
				default:
					return new TextInline { Text = item.PlainText ?? string.Empty };
			}
		}
	}

	private VisualInline MapMention(ProjectionContext context, RichTextMention text) {
		var mention = text.Mention;
		if (mention is null)
			return new TextInline();
		return mention.Type switch {
			"database" when mention.Database is not null => new ReferenceInline { Reference = Reference(context, mention.Database.Id, false) },
			"page" when mention.Page is not null => new ReferenceInline { Reference = Reference(context, mention.Page.Id, false) },
			"user" when mention.User is not null => new PersonInline { Person = MapPerson(mention.User) },
			"date" when mention.Date is not null => new DateInline { Date = MapDate(mention.Date), PlainTextOverride = DateText(mention.Date) },
			_ => new TextInline { Text = text.PlainText ?? string.Empty }
		};
	}

	private R.DatabaseBlock MapDatabase(ProjectionContext context, Database databaseValue, NotionObjectGraph graph) {
		if (context.Purpose == ProjectionPurpose.Html && TryGetResource(databaseValue.Id, out var databaseResource)
			&& _repository.TryGetResourceGraph(databaseResource.ID, out var storedGraph))
			graph = storedGraph;
		var pages = (graph.Children ?? []).Where(node => node is not null).Select(node => FindObject(context, node.ObjectID)).OfType<Page>().ToArray();
		// CMS authentication is hosting policy, never a public database column.
		var cmsDatabaseID = context.Purpose == ProjectionPurpose.Html ? _repository.CMSDatabaseID : null;
		var omitAuthentication = !string.IsNullOrWhiteSpace(cmsDatabaseID) && NormalizeID(databaseValue.Id) == NormalizeID(cmsDatabaseID);
		var columns = pages.SelectMany(pageValue => pageValue.Properties ?? new Dictionary<string, PropertyValue>())
			.Where(pair => !omitAuthentication || pair.Key != Constants.AuthenticationPropertyName)
			.DistinctBy(pair => pair.Key).Select(pair => new R.DatabaseColumn {
				Key = pair.Key,
				Label = pair.Key,
				Type = pair.Value?.Type.ToString() ?? "empty"
			}).ToArray();
		return new R.DatabaseBlock {
			Metadata = new R.Metadata { SourceId = databaseValue.Id, ObjectType = "database", Type = "database" },
			Columns = columns,
			Rows = pages.Select(pageValue => new R.DatabaseRow {
				Metadata = Metadata(context, pageValue, pageValue.Id),
				Cells = columns.Select(column => pageValue.Properties is not null && pageValue.Properties.TryGetValue(column.Key, out var property)
					? MapValue(context, pageValue, property, 0) : new EmptyValue()).ToArray()
			}).ToArray(),
			PlainTextOverride = string.Empty
		};
	}

	private VisualValue MapValue(ProjectionContext context, Page pageValue, PropertyValue property, int depth) {
		if (depth > MaximumDepth)
			return new UnsupportedValue { Diagnostic = "Property nesting exceeds the projection depth." };
		return property switch {
			null => new EmptyValue(),
			CheckboxPropertyValue item => new BooleanValue { Boolean = item.Checkbox },
			CreatedByPropertyValue item => new PeopleValue { People = item.CreatedBy is null ? [] : [MapPerson(item.CreatedBy)] },
			LastEditedByPropertyValue item => new PeopleValue { People = item.LastEditedBy is null ? [] : [MapPerson(item.LastEditedBy)] },
			CreatedTimePropertyValue item => Literal(ChompSeconds(item.CreatedTime)),
			LastEditedTimePropertyValue item => Literal(ChompSeconds(item.LastEditedTime)),
			DatePropertyValue item => new DateValue { Date = MapDate(item.Date) },
			EmailPropertyValue item => new LinkValue { Url = string.IsNullOrWhiteSpace(item.Email) ? null : "mailto:" + item.Email, Label = item.Email ?? string.Empty },
			FilesPropertyValue item => new FilesValue { Files = (item.Files ?? []).Select(file => MapFileValue(context, file)).ToArray() },
			FormulaPropertyValue item => new ComputedValue { Value = MapFormula(item.Formula) },
			MultiSelectPropertyValue item => new ChoiceValue { Choices = (item.MultiSelect ?? []).Where(option => option is not null).Select(option => new R.Choice { Label = option.Name ?? string.Empty, Color = MapColor(option.Color) }).ToArray() },
			NumberPropertyValue item => new NumberValue { Number = item.Number },
			PeoplePropertyValue item => new PeopleValue { People = (item.People ?? []).Select(MapPerson).ToArray() },
			PhoneNumberPropertyValue item => Literal(item.PhoneNumber),
			RelationPropertyValue item => new ReferenceValue { References = (item.Relation ?? []).Where(reference => reference is not null).Select(reference => this.Reference(context, reference.Id, true)).ToArray() },
			RichTextPropertyValue item => new TextValue { Text = MapRichText(context, item.RichText) },
			RollupPropertyValue item => new ComputedValue {
				Value = item.Rollup?.Type switch {
					"number" => new NumberValue { Number = item.Rollup.Number },
					"date" => new DateValue { Date = MapDate(item.Rollup.Date) },
					"array" => new CompoundValue { Values = (item.Rollup.Array ?? []).Select(value => MapValue(context, pageValue, value, depth + 1)).ToArray() },
					_ => new UnsupportedValue { Diagnostic = Diagnostic(item.Rollup) }
				}
			},
			SelectPropertyValue item => item.Select is null ? new EmptyValue() : new ChoiceValue { Choices = [new R.Choice { Label = item.Select.Name ?? string.Empty, Color = MapColor(item.Select.Color) }] },
			StatusPropertyValue item => item.Status is null ? new EmptyValue() : new ChoiceValue { Choices = [new R.Choice { Label = item.Status.Name ?? string.Empty, Color = MapColor(item.Status.Color?.ToString()) }] },
			TitlePropertyValue => new ReferenceValue { IsBlock = true, References = [Reference(context, pageValue.Id, true)] },
			UrlPropertyValue item => new LinkValue { Url = context.Purpose == ProjectionPurpose.Text ? item.Url : ResolveUrl(context.Resource, item.Url, context.OutputDirectory), Label = item.Url ?? string.Empty },
			_ => new UnsupportedValue { Diagnostic = Diagnostic(property) }
		};
	}

	private static VisualValue MapFormula(FormulaValue formula) => formula?.Type switch {
		"string" or "array" => Literal(formula.String),
		"number" => new NumberValue { Number = formula.Number },
		"boolean" => new BooleanValue { Boolean = formula.Boolean },
		"date" => new DateValue { Date = MapDate(formula.Date) },
		_ => new UnsupportedValue { Diagnostic = Diagnostic(formula) }
	};

	private R.MediaBlock MapFileValue(ProjectionContext context, FileObjectWithName file) => new() {
		Type = R.MediaType.File,
		FileName = file?.Name ?? string.Empty,
		Url = context.Purpose == ProjectionPurpose.Text ? string.Empty : ResolveMediaUrl(context.Resource, file, context.OutputDirectory)
	};

	private static TextValue Literal(string text) => new() { Text = [new TextInline { Text = text ?? string.Empty }] };

	private static string ChompSeconds(string value) => value?.EndsWith(":00", StringComparison.Ordinal) == true ? value[..^3] : value ?? string.Empty;

	private static R.Person MapPerson(User userValue) => new() { Name = userValue?.Name ?? string.Empty, Email = userValue?.Person?.Email };

	private static R.Date MapDate(global::Notion.Client.Date dateValueValue) => dateValueValue is null ? null : new() { Start = dateValueValue.Start, End = dateValueValue.End, TimeZone = dateValueValue.TimeZone, IncludeTime = dateValueValue.IncludeTime };

	private static string PersonText(User userValue) => !string.IsNullOrWhiteSpace(userValue?.Name) ? $"{userValue.Name} <{userValue.Person?.Email}>" : userValue?.Person?.Email ?? string.Empty;

	private static string PlainText(IEnumerable<RichTextBase> text) => string.Concat((text ?? []).Select(item => item switch {
		RichTextText run => !string.IsNullOrWhiteSpace(run.Text?.Link?.Url) ? run.Text.Link.Url : run.Text?.Content ?? run.PlainText ?? string.Empty,
		RichTextEquation equation => equation.Equation?.Expression ?? string.Empty,
		RichTextMention mention => mention.Mention?.Type switch {
			"page" when mention.Mention.Page is not null => Environment.NewLine,
			"database" when mention.Mention.Database is not null => Environment.NewLine,
			"user" when mention.Mention.User is not null => PersonText(mention.Mention.User),
			"date" when mention.Mention.Date is not null => DateText(mention.Mention.Date),
			null => string.Empty,
			_ => mention.PlainText ?? string.Empty
		},
		_ => item?.PlainText ?? string.Empty
	}));

	private static string DateText(global::Notion.Client.Date dateValueValue) {
		if (dateValueValue is null)
			return string.Empty;
		var start = dateValueValue.Start is null ? "Empty" : $"{dateValueValue.Start:yyyy-MM-dd HH:mm zzz}";
		return dateValueValue.End is null ? start : $"{start} - {dateValueValue.End:yyyy-MM-dd HH:mm zzz}";
	}
}
