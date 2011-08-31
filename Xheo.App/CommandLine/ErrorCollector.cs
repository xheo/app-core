namespace Xheo.App.v5
{
	/// <summary>
	/// Defines a public delegate for reporting argument parsing errors.
	/// </summary>
	/// <param name="message">
	///		The error message.
	/// </param>
	/// <param name="argument">
	///		The argument that caused the error.
	/// </param>
	public delegate void ErrorCollector( string message, string argument );
}