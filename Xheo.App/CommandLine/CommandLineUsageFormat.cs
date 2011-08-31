using System;

namespace Xheo.App.v5
{
	/// <summary>
	/// 	Defines the usage text layout styles.
	/// </summary>
	[Flags]
	public enum CommandLineUsageFormat
	{
		/// <summary>
		/// 	The default format.
		/// </summary>
		Default = ValuesSameLine,

		/// <summary>
		/// 	The values are printed directly after the command and the help text 
		/// 	follows immediately after on the same line.
		/// </summary>
		ValuesSameLine = 0,

		/// <summary>
		/// 	The parameter values are not printed. The help text is printed immediately
		/// 	on the same line.
		/// </summary>
		NoValues = 1,

		/// <summary>
		/// 	Values are printed immediately after the command. The help text is printed
		/// 	on a new line immediately following.
		/// </summary>
		ValuesSecondLine = 2,

		/// <summary>
		/// 	Mask to exclude non value options.
		/// </summary>
		ValueMask = ValuesSameLine | NoValues | ValuesSecondLine,

		/// <summary>
		/// 	Options on the usage line are compressed to a single [options] statement.
		/// </summary>
		CompressOptions = 4,
	}
}