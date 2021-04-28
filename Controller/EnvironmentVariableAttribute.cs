using System;

namespace PurpleDepot.Controller
{
	[AttributeUsage(AttributeTargets.Property)]
	public class EnvironmentVariableAttribute : Attribute
	{
		public string Name { get; set; }
		public bool Required { get; set; } = false;
		public EnvironmentVariableAttribute(string name, bool required = false)
		{
			Name = name;
			Required = required;
		}
	}
}