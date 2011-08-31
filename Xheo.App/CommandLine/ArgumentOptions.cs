using System;

namespace Xheo.App.v5
{
	/// <summary>
	/// 	Used to control parsing of command line arguments.
	/// </summary>
	[Flags]
	public
		enum ArgumentOptions
	{
		/// <summary>
		/// 	Indicates that the default processing rules should be applied.
		/// </summary>
		None = 0,

		/// <summary>
		/// 	Indicates that this field is required. An error will be displayed
		/// 	if it is not present when parsing arguments.
		/// </summary>
		Required = 1,

		/// <summary>
		/// 	Only valid on array/collection properties.
		/// 	Duplicate values will result in an error.
		/// </summary>
		Unique = 2,

		/// <summary>
		/// 	The default type for non-collection arguments.
		/// 	The argument is not required, but an error will be reported if it is specified more than once.
		/// </summary>
		AtMostOnce = 4,

		/// <summary>
		/// 	For non-collection arguments, when the argument is specified more than
		/// 	once no error is reported and the value of the argument is the last
		/// 	value which occurs in the argument list.
		/// </summary>
		LastOccurrenceWins = 8,

		/// <summary>
		/// 	Indicates the parameter is a common parameter and should be included in the
		/// 	line syntax.
		/// </summary>
		Common = 16,

		/// <summary>
		/// 	Indiates the parameter is an advanced option and should only be displayed with the
		/// 	/?+ help command.
		/// </summary>
		Advanced = 32,

		/// <summary>
		/// 	Indicates that a boolean value is a command and should not display the [+/-] post script.
		/// </summary>
		IsCommand = 64,

		/// <summary>
		/// 	Don't display in the usage strings.
		/// </summary>
		Hidden = 128,
	}
}