using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Xheo.App.v5
{
	/// <summary>
	/// 	Used to parse a command line and retrieve the arguments.
	/// </summary>
	public static class CommandLine
	{
		#region Constants

		private static readonly ArrayList _errors = new ArrayList();

		#endregion

		static CommandLine() { ReportErrorsToErrorStream = false; }

		public static bool HasErrors { get { return _errors.Count > 0; } }
		public static bool ReportErrorsToErrorStream { get; set; }

		///<summary>
		///	Parases the command line and stores the values in the target object.
		///</summary>
		///<param name="target">
		///	The object to populate from the command line.
		///</param>
		///<param name="arguments">
		///	The arguments passed in from the console. If null, attempts to read the
		///	value from the command line.
		///</param>
		///<param name="collector">
		///	A delegate called on each error.
		///</param>
		///<returns>
		///	Returns a value indicating if the parsing was successful.
		///</returns>
		public static bool Parse( object target, string[] arguments, ErrorCollector collector )
		{
			if( target == null )
				return false;

			_errors.Clear();

			if( collector == null )
				collector = DefaultErrorCollector;

			if( arguments == null )
			{
				var tmp = Environment.GetCommandLineArgs();
				arguments = new string[tmp.Length - 1];
				Array.Copy( tmp, 1, arguments, 0, arguments.Length );
			}

			ArrayList records;
			Hashtable nameIndex, shortNameIndex;
			ArgumentRecord defaultArg;
			char lastchar, peek;
			string result;
			var errors = false;
			var helpRequested = false;

			foreach( string arg in arguments )
			{
				var searcharg = arg.ToLower( CultureInfo.InvariantCulture );
				if( searcharg == "/help" || searcharg == "/help+" || searcharg == "-help" || searcharg == "-help+" ||
				    searcharg == "/?" || searcharg == "/?+" || searcharg == "-?" || searcharg == "-?+" )
				{
					helpRequested = true;
					break;
				}
			}

			ParseKnownArguments( target, out records, out nameIndex, out shortNameIndex, out defaultArg );

			for( var index = 0; index < arguments.Length; index++ )
			{
				var useDefault = false;
				var arg = arguments[ index ];

				if( helpRequested &&
				    arg != "/help" && arg != "/help+" && arg != "-help" && arg != "-help+" &&
				    arg != "/?" && arg != "/?+" && arg != "-?" && arg != "-?+" )
					continue;

				if( defaultArg == null && arg.Length < 2 )
					goto unknown;

				if( arg[ 0 ] != '-' && arg[ 0 ] != '/' && defaultArg == null )
					goto unknown;

				var searcharg = arg;


				ArgumentRecord record = null;
				if( arg[ 0 ] == '-' || arg[ 0 ] == '/' )
				{
					record = FindRecord( arg, records, nameIndex, shortNameIndex );
					if( record == null )
						goto unknown;
				}
				else
				{
					record = defaultArg;
					useDefault = true;
				}

				#region Boolean

				if( record.PropertyType == typeof( bool ) || record.PropertyType.Name == "TriBool" )
				{
					lastchar = arg[ arg.Length - 1 ];

					if( lastchar != '-' && lastchar != '+' )
					{
						lastchar = '+';
						if( index < arguments.Length - 1 )
						{
							peek = arguments[ index + 1 ][ 0 ];
							if( peek == '-' || peek == '+' )
							{
								index++;
								lastchar = peek;
							}
						}
					}

					record.SetValue( target, lastchar == '+' );
					if( helpRequested )
						return false;
				}
					#endregion

				else
				{
					if( useDefault )
						result = arg;
					else
					{
						result = GetArgumentValue( arguments, record, collector, ref index );
						if( result == null && record.ArgumentAttribute.RequiredParameters > 0 )
						{
							record.ValueSet = true;
							errors = true;
							continue;
						}
					}

					TypeConverter converter = null;
					if( record.PropertyType.IsArray )
						converter = TypeDescriptor.GetConverter( record.PropertyType.GetElementType() );
					else
						converter = TypeDescriptor.GetConverter( record.PropertyType );

					Debug.Assert( converter != null, "Cannot get a valid converter." );
					string[] validParameters = null;
					if( record.ArgumentAttribute.ValidValues != null )
					{
						validParameters = record.ArgumentAttribute.ValidValues.Split( ';' );
						if( !HasValidValue( result, validParameters[ 0 ] ) )
						{
							errors = true;
							collector( String.Format( "'{0}' is not a valid value for the {1} argument.", result, arg ), arg );
							continue;
						}
					}


					try
					{
						if( result != null )
							errors |= !AssignValue( target, record, converter.ConvertFromString( result ), arg, collector );
						else
							errors |= !AssignValue( target, record, null, arg, collector );
					}
					catch( Exception ex )
					{
						collector( ex.Message, arg );
						errors = true;
					}

					if( record.ArgumentAttribute.Parameters > 1 )
					{
						var parameter = result == null
						                	? 0
						                	: 1;
						while( parameter < record.ArgumentAttribute.Parameters && index < arguments.Length )
						{
							if( index >= arguments.Length - 1 )
								break;

							if( arguments[ index + 1 ][ 0 ] == '/' || arguments[ index + 1 ][ 1 ] == '-' &&
							    FindRecord( arguments[ index + 1 ], records, nameIndex, shortNameIndex ) != null )
								break;

							result = arguments[ index + 1 ];

							if( validParameters != null && validParameters.Length > parameter )
							{
								if( !HasValidValue( result, validParameters[ parameter ] ) )
								{
									errors = true;
									var paramNames = record.ArgumentAttribute.ValueName.Split( ',' );
									var paramName = paramNames.Length > parameter
									                	? paramNames[ parameter ]
									                	: ( "parameter " + parameter );
									collector(
										String.Format( "'{0}' is not a valid value for the {1} parameter of the {2} argument.", result, paramName, arg ),
										arg );
									parameter++;
									index++;
									continue;
								}
							}

							index++;
							parameter++;
							errors |= !AssignValue( target, record, converter.ConvertFromString( result ), arg, collector );
						}

						if( parameter < record.ArgumentAttribute.RequiredParameters ||
						    ( parameter < record.ArgumentAttribute.Parameters && record.ArgumentAttribute.RequiredParameters == -1 ) )
						{
							collector(
								String.Format( CultureInfo.InvariantCulture,
								               "Missing <{0}> parameter for {1} argument",
								               record.ValueNames[ parameter ],
								               arg ),
								arg );
						}

						while( parameter < record.ArgumentAttribute.Parameters )
						{
							AssignValue( target, record, null, arg, collector );
							parameter++;
						}
					}
					continue;
				}

				continue;
				unknown:
				collector( String.Format( CultureInfo.InvariantCulture, "{0} is not a known command line argument.", arg ), arg );
				errors = true;
			}

			foreach( ArgumentRecord record in records )
			{
				if( record.PropertyType.IsArray )
				{
					if( record.ArrayValues.Count > 0 )
						record.SetValue( target, record.ArrayValues.ToArray( record.PropertyType.GetElementType() ) );
				}
				if( ( record.ArgumentAttribute.ArgumentOptions & ArgumentOptions.Required ) != 0 && !record.ValueSet &&
				    record.GetValue( target ) == null )
				{
					if( record.ArgumentAttribute.Default )
					{
						collector(
							String.Format( CultureInfo.InvariantCulture,
							               "The '{0}' argument is required and has not been specified",
							               record.ValueNames[ 0 ] ),
							record.Name );
					}
					else
					{
						collector(
							String.Format( CultureInfo.InvariantCulture,
							               "The '/{0}' argument is required and has not been specified",
							               record.Name ),
							record.Name );
					}
					errors = true;
				}
			}

			return ! errors && ! helpRequested;
		}

		///<summary>
		///	Parases the command line and stores the values in the target object.
		///</summary>
		///<param name="target">
		///	The object to populate from the command line.
		///</param>
		///<param name="arguments">
		///	The arguments passed in from the console. If null, attempts to read the
		///	value from the command line.
		///</param>
		///<returns>
		///	Returns a collection of errors that occured while parsing.
		///</returns>
		public static bool Parse( object target, string[] arguments ) { return Parse( target, arguments, null ); }

		///<summary>
		///	Parases the command line and stores the values in the target object.
		///</summary>
		///<param name="target">
		///	The object to populate from the command line.
		///</param>
		public static bool Parse( object target ) { return Parse( target, null ); }

		///<summary>
		///	Reports an error in post processing. Used only with
		///	<see cref="ShowErrorOrUsage" />
		///	for standard style behavior.
		///</summary>
		///<param name="message">
		///	The error message.
		///</param>
		///<param name="argument">
		///	The argument the error applies to.
		///</param>
		public static void ReportError( string message, string argument ) { _errors.Add( message ); }

		/// <summary>
		/// 	Clears the errors.
		/// </summary>
		public static void ClearErrors() { _errors.Clear(); }

		/// <summary>
		/// 	Makes the usage string for the target object.
		/// </summary>
		/// <param name="target">
		/// 	The target object that usage is to be prepared for.
		/// </param>
		/// <param name="wrap">
		/// 	The width of the output string to wrap at.
		/// </param>
		/// <param name="format">
		/// 	Indicates how the usage text should be formatted.
		/// </param>
		/// <param name="usageSummary">The usage summary.</param>
		/// <returns>
		/// 	Returns the usage string.
		/// </returns>
		public static string MakeUsageString( object target, int wrap, CommandLineUsageFormat format, string usageSummary )
		{
			if( wrap == -1 )
			{
				try
				{
					wrap = Console.BufferWidth;
				}
				catch( IOException )
				{
					wrap = 80;
				}
			}

			var usage = new WrappedStringWriter {Wrap = wrap};


			var args = Environment.GetCommandLineArgs();

			var exe = Path.GetFileName( args[ 0 ] );
			var advanced = Environment.CommandLine.IndexOf( "/?+" ) > -1 || Environment.CommandLine.IndexOf( "-?+" ) > -1 ||
			               Environment.CommandLine.IndexOf( "/help+" ) > -1 || Environment.CommandLine.IndexOf( "-help+" ) > -1;

			ArrayList records;
			Hashtable nameIndex, shortNameIndex;
			ArgumentRecord defaultArg;

			ParseKnownArguments( target, out records, out nameIndex, out shortNameIndex, out defaultArg );


			if( usageSummary != null )
				usage.WriteLine( usageSummary );
			else
			{
				usage.Write( "usage: " );
				usage.Write( exe );
				records.Reverse();

				var compress = ( format & CommandLineUsageFormat.CompressOptions ) != 0;

				if( compress )
					usage.Write( " [options]" );

				foreach( ArgumentRecord record in records )
				{
					if( ( record.ArgumentAttribute.ArgumentOptions & ArgumentOptions.Hidden ) != 0 )
						continue;

					if( ( record.ArgumentAttribute.ArgumentOptions &
					      ( ArgumentOptions.Common | ArgumentOptions.Required ) ) > 0 )
					{
						if( !compress || ( record.ArgumentAttribute.ArgumentOptions & ArgumentOptions.Required ) != 0 ||
						    record.ArgumentAttribute.Default )
						{
							usage.Write( ' ' );
							var optional = ( record.ArgumentAttribute.ArgumentOptions & ArgumentOptions.Common ) > 0 &&
							               ( record.ArgumentAttribute.ArgumentOptions & ArgumentOptions.Required ) == 0;
							if( optional )
								usage.Write( '[' );
							if( ( record.ArgumentAttribute.ArgumentOptions & ArgumentOptions.None ) > 0 ) {}
							else
							{
								var isDefault = record.ArgumentAttribute.Default;

								if( !isDefault || record.PropertyType == typeof( bool ) || record.PropertyType.Name == "TriBool" )
								{
									usage.Write( '/' );
									if( record.ShortName != null && record.ShortName.Length > 0 )
										usage.Write( record.ShortName );
									else
									{
										usage.Write( record.ArgumentAttribute.CaseSensitive
										             	? record.Name
										             	: record.Name.ToLowerInvariant() );
									}
								}

								if( record.PropertyType != typeof( bool ) && record.PropertyType.Name != "TriBool" )
								{
									if( !isDefault && record.ArgumentAttribute.RequiredParameters > 0 )
										usage.Write( ":" );
									for( var ix = 0; ix < record.ArgumentAttribute.RequiredParameters; ix++ )
									{
										if( ix > 0 )
											usage.Write( " " );
										usage.Write( '<' );
										usage.Write( record.ValueNames[ ix ] );
										usage.Write( '>' );
									}
								}
								if( optional )
									usage.Write( ']' );
							}
						}
					}
				}

				usage.WriteLine( "\r\n" );

				records.Reverse();
			}

			string groupName = null;
			var commandWidth = ( format & CommandLineUsageFormat.NoValues ) == CommandLineUsageFormat.NoValues
			                   	? 12
			                   	: 24;
			switch( format & CommandLineUsageFormat.ValueMask )
			{
				case CommandLineUsageFormat.NoValues:
					commandWidth = 12;
					break;
				case CommandLineUsageFormat.ValuesSameLine:
					commandWidth = 24;
					break;
				case CommandLineUsageFormat.ValuesSecondLine:
					commandWidth = wrap - 1;
					break;
			}
			foreach( ArgumentRecord record in records )
			{
				if( ( record.ArgumentAttribute.ArgumentOptions & ArgumentOptions.Hidden ) != 0 )
					continue;

				if( advanced || ( ( record.ArgumentAttribute.ArgumentOptions & ArgumentOptions.Advanced ) == 0 ) )
				{
					if( record.ArgumentAttribute.Group != groupName )
					{
						groupName = record.ArgumentAttribute.Group;
						if( groupName != null && groupName.Length > 0 )
						{
							usage.Write( usage.NewLine );
							usage.Write(
								String.Format( CultureInfo.InvariantCulture, "- {0} -", groupName ),
								usage.Wrap - 1,
								TextAlignment.Center,
								null,
								' ' );
						}
					}
					var optional = ( record.ArgumentAttribute.ArgumentOptions & ArgumentOptions.Required ) == 0;
					var isDefault = record.ArgumentAttribute.Default && record.PropertyType != typeof( bool ) &&
					                record.PropertyType.Name != "TriBool";

					if( !isDefault )
					{
						if( record.PropertyType == typeof( bool ) || record.PropertyType.Name == "TriBool" )
						{
							usage.Write(
								String.Format(
									CultureInfo.InvariantCulture,
									( ( format & CommandLineUsageFormat.ValueMask ) == CommandLineUsageFormat.NoValues ||
									  ( record.ArgumentAttribute.ArgumentOptions & ArgumentOptions.IsCommand ) != 0 )
										? "/{0}"
										: "/{0}[+/-]",
									record.Name ),
								commandWidth,
								TextAlignment.Left,
								"",
								' ' );
						}
						else
						{
							var line = new StringBuilder( wrap );
							line.Append( '/' );
							line.Append( record.Name );

							if( ( format & CommandLineUsageFormat.ValueMask ) != CommandLineUsageFormat.NoValues )
							{
								for( var ix = 0; ix < record.ValueNames.Length; ix++ )
								{
									line.Append( ' ' );
									if( ix >= record.ArgumentAttribute.RequiredParameters )
										line.Append( '[' );
									line.Append( '<' );
									line.Append( record.ValueNames[ ix ] );
									line.Append( '>' );
									if( ix >= record.ArgumentAttribute.RequiredParameters )
										line.Append( ']' );
								}
							}

							usage.Write( line.ToString(), commandWidth, TextAlignment.Left, "", ' ' );
						}
					}
					else
						usage.Write( record.ValueNames[ 0 ], commandWidth, TextAlignment.Left, "", ' ' );
					if( ( format & CommandLineUsageFormat.ValueMask ) == CommandLineUsageFormat.ValuesSecondLine )
						usage.WriteLine( "" );
					switch( format & CommandLineUsageFormat.ValueMask )
					{
						case CommandLineUsageFormat.NoValues:
							usage.Indent = 3;
							break;
						case CommandLineUsageFormat.ValuesSameLine:
							usage.Indent = 6;
							break;
						case CommandLineUsageFormat.ValuesSecondLine:
							usage.Indent = 1;
							break;
					}

					if( ! optional )
						usage.Write( "Required. " );
					usage.Write( record.HelpText );
					if( ! record.ArgumentAttribute.Default && record.SearchShortName != null && record.SearchShortName.Length > 0 &&
					    record.SearchShortName != record.SearchName )
					{
						usage.Write( " (Short form: /" );
						usage.Write( record.SearchShortName );
						usage.Write( ")" );
					}
					usage.Indent = 0;
					usage.Write( usage.NewLine );
				}
			}

			usage.Flush();
			var ur = usage.ToString();

			return ur;
		}

		///<summary>
		///	Makes the usage string for the target object.
		///</summary>
		///<param name="target">
		///	The target object that usage is to be prepared for.
		///</param>
		///<returns>
		///	Returns the usage string.
		///</returns>
		public static string MakeUsageString( object target ) { return MakeUsageString( target, 80, CommandLineUsageFormat.Default, null ); }

		/// <summary>
		/// 	Displays a list of errors or the usage text.
		/// </summary>
		/// <param name="target">
		/// 	The target object that usage is to be prepared for.
		/// </param>
		/// <param name="showHelp">
		/// 	Indicates if the usage help should be displayed.
		/// </param>
		/// <param name="wrap">
		/// 	The width of the output string to wrap at.
		/// </param>
		/// <param name="format">
		/// 	Indicates how the usage text should be formatted.
		/// </param>
		/// <param name="usageSummary">The usage summary.</param>
		/// <returns>
		/// 	Returns 1 if errors occurred during parsing and were displayed, otherwise 0.
		/// </returns>
		public static int ShowErrorOrUsage( object target,
		                                    bool showHelp,
		                                    int wrap,
		                                    CommandLineUsageFormat format,
		                                    string usageSummary )
		{
			var args = Environment.GetCommandLineArgs();
			var exe = Path.GetFileName( args[ 0 ] );

			if( _errors.Count > 0 )
			{
				var tw = ReportErrorsToErrorStream
				         	? Console.Error
				         	: Console.Out;
				var fore = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Red;
				foreach( object err in _errors )
				{
					tw.WriteLine( "ERROR: " + err );
				}
				Console.ForegroundColor = fore;

#if DEBUG
				if( Debugger.IsAttached )
				{
					Console.WriteLine( "Press any key to continue..." );
					try
					{
						Console.Read();
					}
					catch( IOException ) {}
				}
#endif

				return 1;
			}
			else if( showHelp )
				Console.WriteLine( MakeUsageString( target, wrap, format, usageSummary ) );

			return 0;
		}

		///<summary>
		///	Displays a list of errors or the usage text.
		///</summary>
		///<param name="target">
		///	The target object that usage is to be prepared for.
		///</param>
		///<param name="showHelp">
		///	Indicates if the usage help should be displayed.
		///</param>
		///<returns>
		///	Returns 1 if errors were displayed, otherwise 0.
		///</returns>
		public static int ShowErrorOrUsage( object target, bool showHelp ) { return ShowErrorOrUsage( target, showHelp, -1, CommandLineUsageFormat.ValuesSameLine, null ); }

		/// <summary>
		/// 	Makes an error report string for reporting an error in a non-console environment.
		/// </summary>
		public static string MakeErrorReport()
		{
			if( _errors.Count == 0 )
				return null;

			var result = new StringBuilder();
			foreach( object err in _errors )
				result.AppendLine( err.ToString() );

			return result.ToString();
		}

		/// <summary>
		/// 	Resolves a file argument to it's actual path given the
		/// 	<paramref name="rootPath" />
		/// 	and
		/// 	any wildcards.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <param name="rootPath">The root path.</param>
		/// <param name="allowWildcards">
		/// 	if set to
		/// 	<c>true</c>
		/// 	[allow wildcards].
		/// </param>
		/// <returns></returns>
		public static string[] ResolveFileArgument( string value, string rootPath, bool allowWildcards )
		{
			if( value == null || value.Length == 0 ) throw new ArgumentNullException( "value" );
			if( rootPath == null ) rootPath = Environment.CurrentDirectory;

			rootPath = Path.GetFullPath( rootPath );

			if( ! allowWildcards )
				return new[] {Path.GetFullPath( Path.Combine( rootPath, value ) )};

			if( !Directory.Exists( rootPath ) )
				return new string[0];

			return Directory.GetFiles( rootPath, value );
		}

		/// <summary>
		/// 	Resolves a file argument to it's actual path given the
		/// 	<paramref name="rootPath" />
		/// 	and
		/// 	any wildcards.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <param name="rootPath">The root path.</param>
		/// <param name="allowWildcards">
		/// 	if set to
		/// 	<c>true</c>
		/// 	[allow wildcards].
		/// </param>
		/// <returns></returns>
		public static string ResolveFileArgument( string value, string rootPath )
		{
			var result = ResolveFileArgument( value, rootPath, false );
			if( result == null || result.Length == 0 )
				return null;
			return result[ 0 ];
		}

		/// <summary>
		/// 	Resolves a file argument to it's actual path given the
		/// 	<paramref name="rootPath" />
		/// 	and
		/// 	any wildcards.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <param name="rootPath">The root path.</param>
		/// <param name="allowWildcards">
		/// 	if set to
		/// 	<c>true</c>
		/// 	[allow wildcards].
		/// </param>
		/// <returns></returns>
		public static string[] ResolveFileArgument( string[] paths, string rootPath, bool allowWildcards )
		{
			if( paths == null || paths.Length == 0 ) throw new ArgumentNullException( "paths" );
			if( rootPath == null ) rootPath = Environment.CurrentDirectory;

			rootPath = Path.GetFullPath( rootPath );

			var results = new List<string>();

			if( !allowWildcards )
			{
				foreach( string path in paths )
				{
					if( path == null || path.Length == 0 ) continue;

					results.Add( Path.GetFullPath( Path.Combine( rootPath, path ) ) );
				}
			}
			else
			{
				foreach( string path in paths )
				{
					if( path == null || path.Length == 0 ) continue;

					if( path.IndexOf( '*' ) == -1 && path.IndexOf( '?' ) == -1 )
						results.Add( Path.GetFullPath( Path.Combine( rootPath, path ) ) );
					else
					{
						var searchPattern = Path.GetFileName( path );
						var searchFolder = Path.GetDirectoryName( Path.Combine( rootPath, path ) );
						results.AddRange( Directory.GetFiles( searchFolder, searchPattern ) );
					}
				}
			}

			return results.ToArray();
		}

		/// <summary>
		/// 	Shows the copyright.
		/// </summary>
		/// <param name="target">The target.</param>
		public static void ShowCopyright( object target )
		{
			if( target == null ) throw new ArgumentNullException( "target" );

			var asm = target.GetType().Assembly;
			var copyright =
				Attribute.GetCustomAttribute( asm, typeof( AssemblyCopyrightAttribute ) ) as AssemblyCopyrightAttribute;

			string name = null;

			var description =
				Attribute.GetCustomAttribute( asm, typeof( AssemblyDescriptionAttribute ) ) as AssemblyDescriptionAttribute;
			if( description != null )
				name = description.Description;

			if( name == null )
			{
				var title = Attribute.GetCustomAttribute( asm, typeof( AssemblyTitleAttribute ) ) as AssemblyTitleAttribute;
				if( title != null )
					name = title.Title;
			}

			if( name == null )
			{
				var product =
					Attribute.GetCustomAttribute( asm, typeof( AssemblyProductAttribute ) ) as AssemblyProductAttribute;

				if( product != null )
					name = product.Product;
				else
				{
					name = asm.GetName().Name;
					var assemblyName = asm.GetName();
					name += String.Format( " v{0}.{1}", assemblyName.Version.Major, assemblyName.Version.Minor );
				}
			}
			string copyNotice;
			if( copyright != null )
				copyNotice = copyright.Copyright;
			else
			{
				var company = Attribute.GetCustomAttribute( asm, typeof( AssemblyCompanyAttribute ) ) as AssemblyCompanyAttribute;
				var companyName = company == null
				                  	? ""
				                  	: company.Company;
				copyNotice = String.Format( "Copyright (C) {0} {1}. All rights reserved.", DateTime.Now.Year, company );
			}

			copyNotice = copyNotice.Replace( "\u00AE", "" );
			copyNotice = copyNotice.Replace( "\u00A9", "(C)" );
			name = name.Replace( "\u00AE", "" );

			var clr = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine( name );
			Console.WriteLine( copyNotice );
			Console.WriteLine( "" );
			Console.ForegroundColor = clr;
		}

		private static ArgumentRecord FindRecord( string arg, ArrayList records, Hashtable nameIndex, Hashtable shortNameIndex )
		{
			string searcharg;
			ArgumentRecord record = null;
			var bx = arg.IndexOfAny( new[] {':', '+', '-'}, 1 );

			if( bx > -1 )
				searcharg = arg.Substring( 1, bx - 1 );
			else
				searcharg = arg.Substring( 1 );


			var ix = nameIndex[ searcharg ];
			if( ix == null )
				ix = shortNameIndex[ searcharg ];
			if( ix == null )
			{
				searcharg = searcharg.ToLower( CultureInfo.InvariantCulture );
				ix = nameIndex[ searcharg ];
				if( ix == null )
					ix = shortNameIndex[ searcharg ];
				if( ix == null )
					return null;

				record = records[ (int)ix ] as ArgumentRecord;
				if( record.ArgumentAttribute.CaseSensitive )
					return null;
			}
			else
				record = records[ (int)ix ] as ArgumentRecord;

			return record;
		}

		private static bool HasValidValue( string result, string validValuesList )
		{
			if( result == null ) return true;

			if( validValuesList.Length > 1 && validValuesList[ 0 ] == '#' )
				return Regex.IsMatch( result, '^' + validValuesList.Substring( 1 ) + '$', RegexOptions.CultureInvariant );
			else
			{
				var validValues = validValuesList.Split( ',' );
				var matched = false;
				foreach( string validValue in validValues )
				{
					int compare;
					if( validValue.Length > 0 && validValue[ 0 ] == '+' )
					{
						compare = String.Compare( validValue,
						                          1,
						                          result,
						                          0,
						                          Math.Max( result.Length, validValue.Length - 1 ),
						                          false,
						                          CultureInfo.InvariantCulture );
					}
					else
					{
						compare = String.Compare( validValue,
						                          0,
						                          result,
						                          0,
						                          Math.Max( result.Length, validValue.Length ),
						                          true,
						                          CultureInfo.InvariantCulture );
					}
					if( compare == 0 )
					{
						matched = true;
						break;
					}
				}
				return matched;
			}
		}

		private static void DefaultErrorCollector( string message, string argument ) { _errors.Add( message ); }

		private static bool AssignValue( object target,
		                                 ArgumentRecord record,
		                                 object value,
		                                 string arg,
		                                 ErrorCollector collector )
		{
			if( record.PropertyType.IsArray )
			{
				if( ( record.ArgumentAttribute.ArgumentOptions & ArgumentOptions.Unique ) != 0 &&
				    record.ArrayValues.Contains( value ) )
				{
					collector(
						String.Format( CultureInfo.InvariantCulture,
						               "Duplicate value specified for the {0} argument. Value was '{1}'.",
						               arg,
						               value ),
						arg );
					return false;
				}
				record.ArrayValues.Add( value );
			}
			else
			{
				if( record.ValueSet )
				{
					if( ( record.ArgumentAttribute.ArgumentOptions & ArgumentOptions.AtMostOnce ) != 0 )
					{
						collector(
							String.Format( CultureInfo.InvariantCulture,
							               "The {0} argument has already been specified and can be assigned only once.",
							               arg ),
							arg );
						return false;
					}

					if( ( record.ArgumentAttribute.ArgumentOptions & ArgumentOptions.AtMostOnce ) == 0 )
						return true;
				}

				try
				{
					record.SetValue( target, value );
				}
				catch( Exception ex )
				{
					collector( ex.Message, arg );
				}
			}

			return true;
		}

		private static string GetArgumentValue( string[] arguments,
		                                        ArgumentRecord record,
		                                        ErrorCollector collector,
		                                        ref int index )
		{
			var arg = arguments[ index ];
			var required = record.ArgumentAttribute.RequiredParameters > 0;
			string result;
			if( arg.IndexOf( ':' ) > -1 && arg[ arg.Length - 1 ] != ':' )
				result = arg.Substring( arg.IndexOf( ':' ) + 1 );
			else if( index < arguments.Length - 1 )
			{
				result = arguments[ index + 1 ];
				if( result.Length > 0 && ( result[ 0 ] == '/' || result[ 0 ] == '-' ) && ! required )
					result = null;
				else
					index++;
			}
			else
			{
				if( required )
				{
					collector(
						String.Format( CultureInfo.InvariantCulture,
						               "Missing <{0}> parameter for {1} argument",
						               record.ValueNames[ 0 ],
						               arg ),
						arg );
				}
				return null;
			}

			return result;
		}

		private static void ParseKnownArguments( object target,
		                                         out ArrayList records,
		                                         out Hashtable nameIndex,
		                                         out Hashtable shortNameIndex,
		                                         out ArgumentRecord defaultArg )
		{
			var targetType = target.GetType();

			records = new ArrayList();
			nameIndex = new Hashtable();
			shortNameIndex = new Hashtable();

			defaultArg = null;

			var rec = new List<ArgumentRecord>();
			ParseKnownArgumentsOfType( targetType, rec );

			string group = null;
			for( var ix = 0; ix < rec.Count; ix++ )
			{
				var record = rec[ ix ];
				records.Add( record );
				if( record.ArgumentAttribute.Default )
					defaultArg = record;

				nameIndex[ record.SearchName ] = ix;
				if( record.ShortName != null )
					shortNameIndex[ record.SearchShortName ] = ix;

				if( record.ArgumentAttribute.Group == null )
					record.ArgumentAttribute.Group = group;
				else
					group = record.ArgumentAttribute.Group;
			}
		}

		private static void ParseKnownArgumentsOfType( Type targetType, List<ArgumentRecord> records )
		{
			if( targetType != typeof( object ) && targetType.BaseType != null )
				ParseKnownArgumentsOfType( targetType.BaseType, records );


			ParseKnownArgumentsFromMembers(
				targetType.GetProperties( BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static ), records );
			ParseKnownArgumentsFromMembers(
				targetType.GetFields( BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static ), records );
		}

		private static void ParseKnownArgumentsFromMembers( IEnumerable<MemberInfo> members, List<ArgumentRecord> records )
		{
			string group = null;
			var groupIndex = records.Count;

			foreach( MemberInfo member in members )
			{
				var attribute = Attribute.GetCustomAttribute( member, typeof( ArgumentAttribute ) ) as ArgumentAttribute;

				if( attribute == null )
					continue;

				var alreadyProcessed = false;
				foreach( ArgumentRecord argumentRecord in records )
				{
					if( argumentRecord.Member.Name == member.Name )
					{
						alreadyProcessed = true;
						break;
					}
				}
				if( alreadyProcessed )
					continue;

				if( attribute.Group != null && attribute.Group != group )
				{
					groupIndex = records.Count;
					var foundGroup = false;
					for( var ix = 0; ix < records.Count; ix++ )
					{
						var record = records[ ix ];
						if( record.ArgumentAttribute.Group == attribute.Group )
						{
							groupIndex = ix + 1;
							foundGroup = true;
						}
						else if( record.ArgumentAttribute.Group != null && foundGroup )
							break;
						else if( foundGroup )
							groupIndex++;
					}
				}


				records.Insert( groupIndex++, new ArgumentRecord( member ) );
			}
		}

		#region Nested type: ArgumentRecord

		private class ArgumentRecord
		{
			private readonly ArgumentAttribute _argumentAttribute;
			private readonly MemberInfo _member;
			private readonly ArrayList _arrayValues = new ArrayList();
			private bool _valueSet;
			private string _shortName;
			private string _searchName;
			private string _searchShortName;
			private Type _propertyType;
			private string[] _valueNames;
			private string _helpText;

			public ArgumentRecord( MemberInfo member )
			{
				_member = member;
				_argumentAttribute = Attribute.GetCustomAttribute( member, typeof( ArgumentAttribute ) ) as ArgumentAttribute;
			}

			public bool ValueSet { get { return _valueSet; } set { _valueSet = value; } }
			public ArgumentAttribute ArgumentAttribute { get { return _argumentAttribute; } }

			public string Name
			{
				get
				{
					if( _argumentAttribute.Name == null )
						return Member.Name.ToLowerInvariant();
					return _argumentAttribute.Name;
				}
			}

			public string ShortName
			{
				get
				{
					if( _shortName == null )
					{
						_shortName = _argumentAttribute.ShortName;
						if( _shortName == null && Name != null && Name.Length > 0 )
							_shortName = Name.Substring( 0, 1 );
					}
					return _shortName;
				}
				set { _shortName = value; }
			}

			public string SearchName
			{
				get
				{
					return _searchName ?? ( _searchName = _argumentAttribute.CaseSensitive
					                                      	? Name
					                                      	: Name.ToLower( CultureInfo.InvariantCulture ) );
				}
			}

			public string SearchShortName
			{
				get
				{
					if( _searchShortName == null )
					{
						if( ShortName != null )
						{
							_searchShortName = _argumentAttribute.CaseSensitive
							                   	? ShortName
							                   	: ShortName.ToLower( CultureInfo.InvariantCulture );
						}
					}
					return _searchShortName;
				}
			}

			public Type PropertyType
			{
				get
				{
					if( _propertyType == null )
					{
						if( Member is PropertyInfo )
							_propertyType = ( (PropertyInfo)Member ).PropertyType;
						else if( Member is FieldInfo )
							_propertyType = ( (FieldInfo)Member ).FieldType;
					}

					return _propertyType;
				}
			}

			public ArrayList ArrayValues { get { return _arrayValues; } }

			public string[] ValueNames
			{
				get
				{
					if( _valueNames == null )
					{
						if( ArgumentAttribute.ValueName == null )
						{
							if( PropertyType.IsArray )
								_valueNames = new[] {PropertyType.GetElementType().Name.ToLower( CultureInfo.InvariantCulture )};
							else
								_valueNames = new[] {PropertyType.Name.ToLower( CultureInfo.InvariantCulture )};
						}
						else
							_valueNames = ArgumentAttribute.ValueName.Split( ',' );
					}
					return _valueNames;
				}
			}

			public string HelpText
			{
				get
				{
					if( _helpText == null )
					{
						_helpText = ArgumentAttribute.HelpText;
						if( _helpText == null )
						{
							var da = Attribute.GetCustomAttribute( Member, typeof( DescriptionAttribute ) ) as DescriptionAttribute;
							if( da != null )
								_helpText = da.Description;
						}
					}
					return _helpText;
				}
			}

			public MemberInfo Member { get { return _member; } }

			public void SetValue( object target, object value )
			{
				var pi = Member as PropertyInfo;
				if( pi != null )
				{
					if( value != null && pi.PropertyType != value.GetType() )
					{
						var converter = TypeDescriptor.GetConverter( value );
						if( converter.CanConvertTo( pi.PropertyType ) )
							value = converter.ConvertTo( value, pi.PropertyType );
						else
						{
							converter = TypeDescriptor.GetConverter( pi.PropertyType );
							if( converter.CanConvertFrom( value.GetType() ) )
								value = converter.ConvertFrom( null, null, value );
						}
					}
					pi.SetValue( target, value, null );
				}
				else if( Member is FieldInfo )
					( (FieldInfo)Member ).SetValue( target, value );
				_valueSet = true;
			}

			public object GetValue( object target )
			{
				if( Member is PropertyInfo )
					return ( (PropertyInfo)Member ).GetValue( target, null );
				else if( Member is FieldInfo )
					return ( (FieldInfo)Member ).GetValue( target );

				return null;
			}
		}

		#endregion
	}
}