﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Havit.Blazor.Components.Web.Bootstrap.Forms.Internal;
using Havit.Blazor.Components.Web.Bootstrap.Internal;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;

namespace Havit.Blazor.Components.Web.Bootstrap
{
	/// <summary>
	/// Date range input.
	/// </summary>
	public class HxInputDate2<TValue> : HxInputBase<TValue>, IInputWithPlaceholder
	{
		// DO NOT FORGET TO MAINTAIN DOCUMENTATION!
		private static HashSet<Type> supportedTypes = new HashSet<Type> { typeof(DateTime), typeof(DateTimeOffset) };

		public static List<DateItem> DefaultDates { get; set; }

		/// <summary>
		/// When true, uses default date ranges (this month, last month, this year, last year).
		/// </summary>
		[Parameter] public bool UseDefaultDates { get; set; } = true;

		/// <summary>
		/// Custom date ranges. When <see cref="UseDefaultDates"/> is true, these items are used with default items.
		/// </summary>
		[Parameter] public IEnumerable<DateItem> CustomDates { get; set; }

		/// <summary>
		/// Gets or sets the error message used when displaying a parsing error.
		/// Used with String.Format(...), {0} is replaced by Label property, {1} name of bounded property.
		/// </summary>
		[Parameter] public string ParsingErrorMessage { get; set; }

		/// <inheritdoc />
		[Parameter] public string Placeholder { get; set; }

		[Inject] private IStringLocalizer<HxInputDate> StringLocalizer { get; set; }

		public HxInputDate2()
		{
			Type undelyingType = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
			if (!supportedTypes.Contains(undelyingType))
			{
				throw new InvalidOperationException($"Unsupported type {typeof(TValue)}.");
			}
		}

		protected override void BuildRenderInput(RenderTreeBuilder builder)
		{
			builder.OpenComponent(1, typeof(HxInputDateInternal<TValue>));

			builder.AddAttribute(100, nameof(HxInputDateInternal<TValue>.Value), Value);
			builder.AddAttribute(101, nameof(HxInputDateInternal<TValue>.ValueChanged), ValueChanged);
			builder.AddAttribute(102, nameof(HxInputDateInternal<TValue>.ValueExpression), ValueExpression);

			builder.AddAttribute(200, nameof(HxInputDateInternal<TValue>.InputId), InputId);
			builder.AddAttribute(201, nameof(HxInputDateInternal<TValue>.InputCssClass), InputCssClass);
			builder.AddAttribute(202, nameof(HxInputDateInternal<TValue>.EnabledEffective), EnabledEffective);
			builder.AddAttribute(203, nameof(HxInputDateInternal<TValue>.ParsingErrorMessageEffective), GetParsingErrorMessage());
			builder.AddAttribute(204, nameof(HxInputDateInternal<TValue>.Placeholder), Placeholder);
			builder.AddAttribute(205, nameof(HxInputDateInternal<TValue>.CustomDates), GetCustomDates().ToList());

			builder.CloseComponent();
		}

		//protected override void BuildRenderValidationMessage(RenderTreeBuilder builder)
		//{
		//	// NOOP
		//}

		private protected override void BuildRenderInput_AddCommonAttributes(RenderTreeBuilder builder, string typeValue)
		{
			throw new NotSupportedException();
		}

		protected override bool TryParseValueFromString(string value, [MaybeNullWhen(false)] out TValue result, [NotNullWhen(false)] out string validationErrorMessage)
		{
			throw new NotSupportedException();
		}

		private IEnumerable<DateItem> GetCustomDates()
		{
			if (CustomDates != null)
			{
				foreach (DateItem dateItem in CustomDates)
				{
					yield return dateItem;
				}
			}

			if (UseDefaultDates)
			{
				if (DefaultDates != null)
				{
					foreach (DateItem defaultDateItem in DefaultDates)
					{
						yield return defaultDateItem;
					}
				}
				else
				{
					DateTime today = DateTime.Today;

					yield return new DateItem { Label = StringLocalizer["Today"], Date = today };
				}
			}
		}

		/// <summary>
		/// Returns message for a parsing error.
		/// </summary>
		protected virtual string GetParsingErrorMessage()
		{
			var message = !String.IsNullOrEmpty(ParsingErrorMessage)
				? ParsingErrorMessage
				: StringLocalizer["ParsingErrorMessage"];
			return String.Format(message, Label, FieldIdentifier.FieldName);
		}
	}
}