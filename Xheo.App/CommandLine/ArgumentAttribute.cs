using System;

namespace Xheo.App.v5
{
	/// <summary>
	/// 	Defines the properties for the command line argument.
	/// </summary>
	[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true )]
	public sealed class ArgumentAttribute : Attribute
	{
		private readonly ArgumentOptions _argumentOptions = ArgumentOptions.None;
		private int _parameters = 1;
		private int _requiredParameters = 1;

		/// <summary>
		/// 	Initializes a new instance of the ArgumentAttribute class.
		/// </summary>
		public ArgumentAttribute() { }

		/// 
		///<summary>
		///	Initializes a new instance of the ArgumentAttribute class.
		///</summary>
		///<param name="argumentOptions">
		///	The options for the argument.
		///</param>
		public ArgumentAttribute( ArgumentOptions argumentOptions ) { _argumentOptions = argumentOptions; }

		/// <summary>
		/// 	Gets or sets the name of the command line argument.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// 	Gets or sets the short name for the argument.
		/// </summary>
		public string ShortName { get; set; }

		/// <summary>
		/// 	Gets or sets help text to display for the option in the usage message.
		/// </summary>
		public string HelpText { get; set; }

		/// <summary>
		/// 	Gets or sets the name of the option group to include the option in.
		/// </summary>
		public string Group { get; set; }

		/// <summary>
		/// 	Gets or sets a value that indicates if this is the default property. Only one
		/// 	property can be marked as default.
		/// </summary>
		public bool Default { get; set; }

		/// <summary>
		/// 	Gets or sets a value that indicates if the property is case sensitive.
		/// </summary>
		public bool CaseSensitive { get; set; }

		/// <summary>
		/// 	Gets or sets parsing options for the argument.
		/// </summary>
		public ArgumentOptions ArgumentOptions { get { return _argumentOptions; } }

		/// <summary>
		/// 	Gets or sets the name to use when describing the argument value. Use comma separated values to specify more than one value.
		/// </summary>
		public string ValueName { get; set; }

		/// <summary>
		/// 	Gets or sets the number of parameters expected for the argument. Argument must
		/// 	be of type string[].
		/// </summary>
		public int Parameters { get { return _parameters; } set { _parameters = value; } }

		/// <summary>
		/// 	Gets or sets the number of required parameters if multiple parameters are
		/// 	allowed. A value of -1 indicates all are required.
		/// </summary>
		public int RequiredParameters { get { return _requiredParameters; } set { _requiredParameters = value; } }

		/// 
		///<summary>
		///	Gets or sets a comma separated list of valid values for the option.
		///</summary>
		///<remarks>
		///	Values should not contain spaces and should be separated by commas. Value
		///	lists for additional parameters should be separated by a semi-colon. All
		///	values are treated as case-insensitive, to enforce case, prefix the value with
		///	a + sign. Use a # sign to treat the list of values as a regular expression.
		///</remarks>
		public string ValidValues { get; set; }
	}
}