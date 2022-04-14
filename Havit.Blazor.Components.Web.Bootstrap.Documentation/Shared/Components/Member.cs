﻿using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using LoxSmoke.DocXml;

namespace Havit.Blazor.Components.Web.Bootstrap.Documentation.Shared.Components
{
	public abstract class Member
	{
		private Type enclosingType;

		private bool generic;

		protected string TryFormatComment(string comment, Type enclosingType = null)
		{
			this.enclosingType = enclosingType;

			try
			{
				if (string.IsNullOrEmpty(comment))
				{
					return string.Empty;
				}

				// <c>
				{
					Regex regex = new("<c>");
					comment = regex.Replace(comment, "<code>");
					regex = new("</c>");
					comment = regex.Replace(comment, "</code>");
				}

				// <see cref=""/> + other <see> variantions
				{
					Regex regex = new("<see");
					comment = regex.Replace(comment, "<a");

					regex = new("<a cref=");
					comment = regex.Replace(comment, "<code><a cref=");

					regex = new("</see>");
					comment = regex.Replace(comment, "</a>");

					regex = new("cref=\"([A-Za-z\\.:`\\d])+");
					var matches = regex.Matches(comment);

					foreach (var match in matches)
					{
						string link = match.ToString().Split('\"').LastOrDefault(); // get the part in the quotes (value of the cref attribute)
						link = PrepareLinkForFullLinkGeneration(link);
						string[] splitLink = link.Split('.');

						regex = new("cref=\"([A-Za-z\\.:`\\d])+\" ?/>"); // find this part of the element (beggining already replaced): cref="P:System.Text.Regex.Property" />
						comment = regex.Replace(comment, GenerateFullLink(splitLink, link), 1); // replace the above with a generated link to the documentation
					}
				}
			}
			catch
			{
				// NOOP
			}

			return comment;
		}

		private string PrepareLinkForFullLinkGeneration(string link)
		{
			generic = IsGeneric(link); // this information is used later to generate a Microsoft documentation link

			Regex regex = new("`\\d");
			return regex.Replace(link, "");
		}

		private string GenerateFullLink(string[] splitLink, string fullLink)
		{
			if (splitLink is null || splitLink.Length == 0)
			{
				return string.Empty;
			}

			string supportClassesResultLink = HandleSupportClasses(splitLink, fullLink);
			if (supportClassesResultLink is not null)
			{
				return supportClassesResultLink;
			}

			if (splitLink[0].Contains("Havit"))
			{
				return GenerateHavitDocumentationLink(splitLink);
			}

			if (splitLink[0].Contains("Microsoft") || splitLink[0].Contains("System"))
			{
				return GenerateMicrosoftDocumentationLink(splitLink);
			}

			throw new NotSupportedException();
		}

		private string HandleSupportClasses(string[] splitLink, string fullLink)
		{
			if (IsEnum(splitLink))
			{
				string internalTypeLink = ComponentApiDoc.GenerateLinkForInternalType(splitLink[^2], false, $"{splitLink[^2]}.{splitLink[^1]}");
				if (internalTypeLink is not null)
				{
					return internalTypeLink;
				}
			}

			if (splitLink[^1] == "Defaults")
			{
				string internalTypeLink = ComponentApiDoc.GenerateLinkForInternalType($"{splitLink[^2].Remove(0, 2)}{splitLink[^1]}", false, $"{splitLink[^2]}.{splitLink[^1]}"); // We have to generate a type name suitable for the support type page.
				if (internalTypeLink is not null)
				{
					return internalTypeLink;
				}
			}

			return null;
		}

		private string GenerateHavitDocumentationLink(string[] splitLink)
		{
			bool containsHx = splitLink.Any(t => t.Contains("hx", StringComparison.OrdinalIgnoreCase));
			string seeName = "";

			string fullLink = "";

			bool isType = IsType(splitLink);
			bool isProperty = IsProperty(splitLink);

			if (isType)
			{
				for (int i = splitLink.Length - 1; i >= 0; i--) // loop through the array from the back, prepend the current part, and stop once the component name is reached
				{
					fullLink = $"{splitLink[i]}/{fullLink}";

					if (splitLink[i].Contains("Hx"))
					{
						containsHx = true;
						seeName = splitLink[i];
						break;
					}
				}
			}
			else if (isProperty)
			{
				if (splitLink.Length >= 2)
				{
					fullLink = $"{splitLink[^2]}#{splitLink[^1]}";
					if (enclosingType is not null && PrepareLinkForFullLinkGeneration(enclosingType.Name) == splitLink[^2])
					{
						seeName = $"{splitLink[^1]}";
					}
					else
					{
						seeName = $"{splitLink[^2]}.{splitLink[^1]}";
					}
				}
			}
			else
			{
				if (splitLink.Length >= 2)
				{
					fullLink = splitLink[^2];
					seeName = $"{splitLink[^2]}.{splitLink[^1]}";
				}
			}

			// Default case: if "Hx" wasn't found anywhere, it couldn't be detected what part to use in the link, so the default part will be chosen
			if (fullLink.Length == 0 || (containsHx == false && isType))
			{
				fullLink = splitLink[^1];
				seeName = splitLink[^1];
			}

			if (enclosingType is null || enclosingType.FullName.Contains("Hx") || !isProperty)
			{
				fullLink = $"href=\"/components/{fullLink}";
			}
			else
			{
				fullLink = $"href=\"/types/{fullLink}";
			}
			fullLink += $"\">{seeName}</a></code>";
			return fullLink;
		}

		private string GenerateMicrosoftDocumentationLink(string[] splitLink)
		{
			bool isType = IsType(splitLink);

			string fullLink = "";
			string seeName = splitLink[^1];
			if (isType)
			{
				string link = ConcatenateStringArray(splitLink);
				fullLink = link.Split(':')[^1];
			}

			string genericTypeLinkSuffix = generic ? "-1" : "";

			fullLink = $"href=\"https://docs.microsoft.com/en-us/dotnet/api/{fullLink}{genericTypeLinkSuffix}";
			fullLink += $"\">{seeName}</a></code>";
			return fullLink;
		}

		private bool IsProperty(string[] splitLink)
		{
			return splitLink[0][0] == 'P';
		}
		private bool IsType(string[] splitLink)
		{
			return splitLink[0][0] == 'T';
		}
		private bool IsEnum(string[] splitLink)
		{
			return splitLink[0][0] == 'F';
		}

		private bool IsGeneric(string link)
		{
			return link.Contains('`');
		}

		private string ConcatenateStringArray(string[] stringArray)
		{
			StringBuilder sb = new StringBuilder();
			foreach (var text in stringArray)
			{
				sb.Append(text);
				sb.Append(".");
			}

			sb.Remove(sb.Length - 1, 1);

			return sb.ToString();
		}
	}

	public class ClassMember : Member
	{
		public TypeComments Comments
		{
			get => comments;
			set
			{
				TypeComments inputComments = value;
				try { inputComments.Summary = TryFormatComment(inputComments.Summary); } catch { }
				comments = inputComments;
			}
		}
		private TypeComments comments;
	}

	public class Property : Member
	{
		public PropertyInfo PropertyInfo { get; set; }
		public bool EditorRequired { get; set; }

		public CommonComments Comments
		{
			set
			{
				CommonComments inputComments = value;
				try { inputComments.Summary = TryFormatComment(inputComments.Summary, PropertyInfo.DeclaringType); } catch { }
				comments = inputComments;
			}
			get
			{
				return comments;
			}
		}
		private CommonComments comments;
	}

	public class Method : Member
	{
		public MethodInfo MethodInfo { get; set; }

		public MethodComments Comments
		{
			set
			{
				MethodComments inputComments = value;
				try { inputComments.Summary = TryFormatComment(inputComments.Summary); } catch { }
				comments = inputComments;
			}
			get
			{
				return comments;
			}
		}
		private MethodComments comments;

		public string GetParameters()
		{
			StringBuilder concatenatedParameters = new StringBuilder();
			var parameters = MethodInfo.GetParameters();

			if (parameters is null || parameters.Length == 0)
			{
				return "()";
			}

			concatenatedParameters.Append("(");
			foreach (var parameter in parameters)
			{
				concatenatedParameters.Append($"{ComponentApiDoc.FormatType(parameter.ParameterType)} {parameter.Name}, ");
			}
			concatenatedParameters.Remove(concatenatedParameters.Length - 2, 2);
			concatenatedParameters.Append(")");

			return concatenatedParameters.ToString();
		}
	}

	public class EnumMember : Member
	{
		public int Index { get; set; }
		public string Name { get; set; }

		public string Summary
		{
			get
			{
				return summary;
			}
			set
			{
				try
				{
					summary = TryFormatComment(value);
				}
				catch
				{
					summary = value;
				}

			}
		}
		private string summary;
	}
}
