﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Havit.Blazor.Components.Web.Bootstrap.Forms;
using Havit.Blazor.Components.Web.Infrastructure;
using Havit.Diagnostics.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Havit.Blazor.Components.Web.Bootstrap.Internal
{
	/// <summary>
	/// Internal implementation for <see cref="HxInputTags"/>.
	/// </summary>
	public partial class HxInputTagsInternal
	{
		/// <summary>
		/// Indicates whether you are restricted to suggested items only (<c>false</c>).
		/// Default is <c>true</c> (you can type your own tags).
		/// </summary>
		[Parameter] public bool AllowCustomTags { get; set; } = true;

		[Parameter] public List<string> Value { get; set; }
		protected List<string> ValueEffective => Value ?? new();
		[Parameter] public EventCallback<List<string>> ValueChanged { get; set; }

		[Parameter] public InputTagsDataProviderDelegate DataProvider { get; set; }

		/// <summary>
		/// Minimal number of characters to start suggesting. Default is <c>2</c>.
		/// </summary>
		[Parameter] public int SuggestMinimumLength { get; set; } = 2;

		/// <summary>
		/// Characters, when typed, divide the current input into separate tags.
		/// Default is comma, semicolon and space.
		/// </summary>
		[Parameter] public List<char> Delimiters { get; set; } = new() { ',', ';', ' ' };

		/// <summary>
		/// Indicates whether the add-icon (+) should be displayed.
		/// Default is <c>false</c>.
		/// </summary>
		[Parameter] public bool ShowAddButtonEffective { get; set; } = false;

		/// <summary>
		/// Optional text for the add-button.
		/// Displayed only when there are no tags (the <see cref="Value"/> is empty).
		/// Default is <c>null</c> (none).
		/// </summary>
		[Parameter] public string AddButtonText { get; set; }

		/// <summary>
		/// Indicates whether a "naked" variant should be displayed (no border).
		/// Default is <c>false</c>.
		/// Consider enabling <see cref="HxInputTags.ShowAddButton"/> when using <c>Naked</c>.
		/// </summary>
		[Parameter] public bool Naked { get; set; } = false;

		/// <summary>
		/// Short hint displayed in the input field before the user enters a value.
		/// </summary>
		[Parameter] public string Placeholder { get; set; }

		/// <summary>
		/// Background color of the tag (also used for the AddButton).
		/// </summary>
		[Parameter] public ThemeColor TagBackgroundColor { get; set; }

		/// <summary>
		/// Color of the tag text (also used for the AddButtonText and icons).
		/// </summary>
		[Parameter] public ThemeColor TagTextColor { get; set; }

		[Parameter] public int SuggestDelay { get; set; } = 300;

		[Parameter] public string InputCssClass { get; set; }

		[Parameter] public string InputId { get; set; }

		[Parameter] public bool EnabledEffective { get; set; } = true;

		[Parameter] public LabelType LabelTypeEffective { get; set; }

		[Parameter] public InputSize InputSizeEffective { get; set; }

		/// <summary>
		/// Offset between dropdown input and dropdown menu
		/// </summary>
		[Parameter] public (int X, int Y) InputOffset { get; set; }

		/// <summary>
		/// Any additional CSS class to apply.
		/// </summary>
		[Parameter] public string CssClass { get; set; }

		[Inject] protected IJSRuntime JSRuntime { get; set; }

		private string dropdownId = "hx" + Guid.NewGuid().ToString("N");
		private System.Timers.Timer timer;
		private string userInput = String.Empty;
		private CancellationTokenSource cancellationTokenSource;
		private List<string> suggestions;
		private bool isDropdownOpened = false;
		private bool blurInProgress;
		private bool currentlyFocused;
		private bool mouseDownFocus;
		private IJSObjectReference jsModule;
		private HxInputTagsAutosuggestInput autosuggestInput;
		private bool dataProviderInProgress;
		private DotNetObjectReference<HxInputTagsInternal> dotnetObjectReference;

		public HxInputTagsInternal()
		{
			dotnetObjectReference = DotNetObjectReference.Create(this);
		}

		private async Task AddTagWithEventCallbackAsync(string tag)
		{
			if ((Value != null) && Value.Contains(tag))
			{
				return;
			}

			if (Value == null)
			{
				Value = new List<string> { tag };
			}
			else
			{
				Value = new List<string>(Value); // do not change the insntace, create a copy!
				Value.Add(tag);
			}
			await ValueChanged.InvokeAsync(Value);
		}

		private async Task RemoveTagWithEventCallbackAsync(string tag)
		{
			if (Value == null)
			{
				return;
			}

			Value = Value.Except(new string[] { tag }).ToList();
			await ValueChanged.InvokeAsync(Value);
		}

		protected override void OnParametersSet()
		{
			base.OnParametersSet();

			// TODO Do we need DataProvider? For free-entry probably not...
			Contract.Requires<InvalidOperationException>(DataProvider != null, $"{GetType()} requires a {nameof(DataProvider)} parameter.");
		}

		public async ValueTask FocusAsync()
		{
			await autosuggestInput.FocusAsync();
		}

		protected async Task HandleInputKeyDown(KeyboardEventArgs args)
		{
			if ((args.Code == "Backspace") && String.IsNullOrWhiteSpace(userInput) && ValueEffective.Any())
			{
				await RemoveTagWithEventCallbackAsync(ValueEffective.Last());
			}
		}

		private async Task HandleInputInput(string newUserInput)
		{
			// user changes an input
			userInput = newUserInput ?? String.Empty;

			timer?.Stop(); // if waiting for an interval, stop it
			cancellationTokenSource?.Cancel(); // if already loading data, cancel it
			dataProviderInProgress = false; // data provider is no more in progress				 

			// tag delimiters
			await TryProcessCustomTagsAsync(keepLastTagForSuggestion: true);

			// start new time interval
			if (userInput.Length >= SuggestMinimumLength)
			{
				if (SuggestDelay == 0)
				{
					await UpdateSuggestionsAsync();
				}
				else
				{
					if (timer == null)
					{
						timer = new System.Timers.Timer();
						timer.AutoReset = false; // just once
						timer.Elapsed += HandleTimerElapsed;
					}
					timer.Interval = SuggestDelay;
					timer.Start();
				}
			}
			else
			{
				// or close a dropdown
				suggestions = null;
				await TryDestroyDropdownAsync();
			}
		}

		private async void HandleTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			// when a time interval reached, update suggestions
			await InvokeAsync(async () =>
			{
				await UpdateSuggestionsAsync();
			});
		}

		private void HandleInputMouseDown()
		{
			mouseDownFocus = true;
		}

		private async Task HandleInputFocus()
		{
			// when an input gets focus, close a dropdown
			if (!currentlyFocused)
			{
				await TryDestroyDropdownAsync();
			}
			currentlyFocused = true;

			if (SuggestMinimumLength == 0)
			{
				await UpdateSuggestionsAsync(bypassShow: mouseDownFocus);
			}
			mouseDownFocus = false;
		}

		// Kvůli updatovanání HTML a kolizi s bootstrap Dropdown nesmíme v InputBlur přerenderovat html!
		private Task HandleInputBlur()
		{
			currentlyFocused = false;
			// when user clicks back button in browser this method can be called after it is disposed!
			blurInProgress = true;
			timer?.Stop(); // if waiting for an interval, stop it
			cancellationTokenSource?.Cancel(); // if waiting for an interval, stop it
			dataProviderInProgress = false; // data provider is no more in progress				 

			return Task.CompletedTask;
		}

		private async Task TryProcessCustomTagsAsync(bool keepLastTagForSuggestion = false)
		{
			if (!AllowCustomTags)
			{
				return;
			}

			// tags before last delimiter
			char[] delimitersArray = Delimiters.ToArray();
			var delimiterIndex = userInput.IndexOfAny(delimitersArray);
			while (delimiterIndex >= 0)
			{
				var tag = userInput.Substring(0, delimiterIndex).Trim(delimitersArray);
				userInput = userInput.Substring(delimiterIndex).TrimStart(delimitersArray);

				if (!String.IsNullOrWhiteSpace(tag))
				{
					await AddTagWithEventCallbackAsync(tag);
				}

				delimiterIndex = userInput.IndexOfAny(delimitersArray);
			}

			// last tag
			if (!keepLastTagForSuggestion)
			{
				var newTag = userInput?.Trim(delimitersArray);
				if (!String.IsNullOrWhiteSpace(newTag))
				{
					await AddTagWithEventCallbackAsync(newTag);
				}
				userInput = String.Empty;
			}
		}

		private async Task UpdateSuggestionsAsync(bool bypassShow = false)
		{
			// Cancelation is performed in HandleInputInput method
			cancellationTokenSource?.Dispose();

			cancellationTokenSource = new CancellationTokenSource();
			CancellationToken cancellationToken = cancellationTokenSource.Token;

			// TODO Do we want spinnner? Configurable?
			//dataProviderInProgress = true;
			//StateHasChanged();

			InputTagsDataProviderRequest request = new InputTagsDataProviderRequest
			{
				UserInput = userInput,
				CancellationToken = cancellationToken
			};

			InputTagsDataProviderResult result;
			try
			{
				result = await DataProvider.Invoke(request);
			}
			catch (OperationCanceledException) // gRPC stack does not set the operationFailedException.CancellationToken, do not check in when-clause
			{
				return;
			}

			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}

			dataProviderInProgress = false;
			suggestions = result.Data.ToList();

			if (suggestions?.Any() ?? false)
			{
				await OpenDropdownAsync(bypassShow);
			}
			else
			{
				await TryDestroyDropdownAsync();
			}

			StateHasChanged();
		}

		private async Task HandleItemClick(string tag)
		{
			// user clicked on an item in the "dropdown".
			userInput = String.Empty;
			await AddTagWithEventCallbackAsync(tag);

			if (!currentlyFocused)
			{
				await FocusAsync();
			}
		}

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			await base.OnAfterRenderAsync(firstRender);

			if (blurInProgress)
			{
				blurInProgress = false;
				if (!isDropdownOpened)
				{
					await TryProcessCustomTagsAsync();
					userInput = String.Empty;
					StateHasChanged();
				}
			}
		}

		private async Task OpenDropdownAsync(bool bypassShow = false)
		{
			if (!isDropdownOpened)
			{
				await EnsureJsModuleAsync();
				await jsModule.InvokeVoidAsync("open", autosuggestInput.InputElement, dotnetObjectReference, bypassShow);
				isDropdownOpened = true;
			}
		}

		private async Task TryDestroyDropdownAsync()
		{
			if (isDropdownOpened)
			{
				await EnsureJsModuleAsync();

				await jsModule.InvokeVoidAsync("destroy", autosuggestInput.InputElement);
				isDropdownOpened = false;
			}
		}

		private async Task EnsureJsModuleAsync()
		{
			jsModule ??= await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/Havit.Blazor.Components.Web.Bootstrap/" + nameof(HxInputTags) + ".js");
		}

		[JSInvokable("HxInputTagsInternal_HandleDropdownHidden")]
		public async Task HandleDropdownHidden()
		{
			isDropdownOpened = false;

			if (!currentlyFocused)
			{
				await TryProcessCustomTagsAsync();
				userInput = String.Empty;
				StateHasChanged();
			}
		}

		protected async Task HandleRemoveClickAsync(string tag)
		{
			await RemoveTagWithEventCallbackAsync(tag);
		}

		protected string GetFormControlCssClasses()
		{
			if (Naked)
			{
				return null;
			}
			return CssClassHelper.Combine("form-control", this.InputSizeEffective.AsFormControlCssClass());
		}

		protected string GetNakedCssClasses()
		{
			if (!Naked)
			{
				return null;
			}
			return CssClassHelper.Combine("hx-input-tags-naked", this.InputSizeEffective switch
			{
				InputSize.Regular => null,
				InputSize.Small => "hx-input-tags-naked-sm",
				InputSize.Large => "hx-input-tags-naked-lg",
				_ => throw new InvalidOperationException($"Unknown {nameof(InputSize)} value {this.InputSizeEffective}.")
			});
		}

		public async ValueTask DisposeAsync()
		{
			timer?.Dispose();
			timer = null;
			cancellationTokenSource?.Dispose();
			cancellationTokenSource = null;

			dotnetObjectReference.Dispose();

			if (jsModule != null)
			{
				await jsModule.DisposeAsync();
			}
		}
	}
}