using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Xheo.App.v5
{
	public class WrappedStringWriter : StringWriter
	{
		#region Constants

		private static readonly Regex _indentReplace = new Regex( @"(\n)(?<capture>.)",
		                                                          RegexOptions.Compiled );

		private static readonly Regex _htmlReplace = new Regex( @"</??[a-z][a-z0-9\-:]*.*?>",
		                                                        RegexOptions.IgnoreCase | RegexOptions.Compiled |
		                                                        RegexOptions.Singleline );

		#endregion

		private readonly StringBuilder _wrapBuffer = new StringBuilder( 180 );
		private readonly char[] _whitespace = new[] {'\r', '\n', '\t', '\f', '\v', ' '};
		private int _tabWidth = -1;
		private string _tab;
		private int _indent;
		private string _indentText;
		private char _lastChar = (char)0;
		private bool _indentOnNext;

		public WrappedStringWriter() { Wrap = -1; }

		public int Wrap { get; set; }

		public int TabWidth
		{
			get
			{
				return _tabWidth < 0
				       	? 4
				       	: _tabWidth;
			}
			set
			{
				if( value != _tabWidth )
				{
					_tabWidth = value;
					_tab = null;
				}
			}
		}

		public int Indent
		{
			get { return _indent; }
			set
			{
				_indent = value < 0
				          	? 0
				          	: value;

				_indentText = null;
				if( Wrap > 0 && _wrapBuffer.Length == 0 )
					_indentOnNext = true;
				else
					_indentOnNext = false;
			}
		}

		public override void Write( char value )
		{
			ProcessIndent();
			if( Wrap == -1 )
				base.Write( value );
			else
			{
				_wrapBuffer.Append( value );
				ProcessWrap( CoreNewLine[ CoreNewLine.Length - 1 ] == value
				             	? 1
				             	: 0 );
			}

			if(
				( CoreNewLine.Length == 1 && value == CoreNewLine[ 0 ] ) ||
				( ( value == CoreNewLine[ CoreNewLine.Length - 1 ] ) && _lastChar == CoreNewLine[ 0 ] )
				)
				_indentOnNext = true;
			_lastChar = value;
		}

		public override void Write( string value )
		{
			ProcessIndent();
			if( value == null ) return;

			if( Wrap == -1 )
			{
				value = _indentReplace.Replace( value, "${1}" + _indentText + "${2}" );
				base.Write( value );
			}
			else
			{
				_wrapBuffer.Append( value );
				ProcessWrap( value.IndexOf( NewLine ) > -1
				             	? 1
				             	: -1 );
			}
			if( value.Length > 0 )
				_lastChar = value[ value.Length - 1 ];
			if( value.Length > NewLine.Length )
			{
				_indentOnNext =
					String.Compare( NewLine,
					                0,
					                value,
					                value.Length - NewLine.Length,
					                NewLine.Length,
					                false,
					                CultureInfo.InvariantCulture ) == 0;
			}
		}

		public override void Write( char[] buffer, int index, int count )
		{
			ProcessIndent();
			if( Wrap == -1 )
			{
				if( _indent > 0 )
					Write( new string( buffer, index, count ) );
				else
					base.Write( buffer, index, count );
			}
			else
			{
				if( buffer == null )
					return;
				if( index < 0 || index > buffer.Length || count < 0 ||
				    index + count > buffer.Length )
					throw new IndexOutOfRangeException();
				_wrapBuffer.Append( buffer, index, count );
				ProcessWrap( 0 );
			}

			_lastChar = buffer[ index + count - 1 ];
		}

		public override void Flush()
		{
			if( Wrap > 0 )
			{
				base.Write( _wrapBuffer.ToString() );
				_wrapBuffer.Length = 0;
			}
			base.Flush();
		}

		public void Write( string value, int width, TextAlignment alignment, string trimText, char padChar )
		{
			if( value == null )
				value = "";
			if( trimText == null )
				trimText = "...";
			if( value.Length > width )
			{
				value = value.Substring( 0, width - trimText.Length ) + trimText;
				Write( value );
				return;
			}

			var padding = width - value.Length;
			switch( alignment )
			{
				case TextAlignment.Left:
					Write( value );
					Write( new string( padChar, padding ) );
					break;
				case TextAlignment.Center:
					var left = padding/2;
					Write( new string( padChar, left ) );
					Write( value );
					Write( new string( padChar, padding - left ) );
					break;
				case TextAlignment.Right:
					Write( new string( padChar, padding ) );
					Write( value );
					break;
			}
		}

		public void Write( string value, int width, TextAlignment alignment ) { Write( value, width, alignment, "...", ' ' ); }

		public void Write( string value, int width ) { Write( value, width, TextAlignment.Left ); }

		public static string StripHtml( string value ) { return _htmlReplace.Replace( value, String.Empty ); }

		protected void ProcessWrap( int addedNewLine )
		{
			var hasNewLine = addedNewLine == 1;
			var hasTrailingNewLine = false;
			var wrap = Wrap < 0
			           	? int.MaxValue
			           	: Wrap;
			MakeIndentText();
			var indentwrap = wrap - _indentText.Length;

			if( addedNewLine == 0 )
				hasNewLine = _wrapBuffer.ToString().IndexOf( NewLine ) > -1;

			string[] lines;

			var buffer = _wrapBuffer.ToString();
			if( buffer.Length > NewLine.Length )
			{
				hasTrailingNewLine =
					String.Compare( NewLine,
					                0,
					                buffer,
					                buffer.Length - NewLine.Length,
					                NewLine.Length,
					                false,
					                CultureInfo.InvariantCulture ) == 0;
			}


			if( hasNewLine )
				lines = Regex.Split( buffer, NewLine, RegexOptions.Compiled );
			else
			{
				if( _wrapBuffer.Length < wrap )
					return;
				lines = new[] {buffer};
			}


			var tab = _tab ?? new string( ' ', TabWidth );
			_wrapBuffer.Length = 0;
			for( var index = 0; index < lines.Length; index++ )
			{
				var line = lines[ index ];
				if( line != null )
				{
					if( line.Length == 0 && index == lines.Length - 1 && hasTrailingNewLine )
						continue;
					line = line.Replace( "\t", tab );
					if( index > 0 )
						line = _indentText + line;
					if( line.Length <= wrap )
					{
						if( index == lines.Length - 1 && !hasTrailingNewLine )
						{
							_wrapBuffer.Append( line );
							break;
						}
						else
						{
							if( _wrapBuffer.Length > 0 )
							{
								WriteLineInternal( _wrapBuffer.ToString() );
								_wrapBuffer.Length = 0;
							}
							WriteLineInternal( line );
						}
					}
					else
					{
						var wordindex = 0;
						var nextspace = 0;
						var column = 0;

						var buildline = new StringBuilder( Wrap*2 );

						while( wordindex > -1 && wordindex < line.Length )
						{
							nextspace = line.IndexOfAny( _whitespace, wordindex );
							if( nextspace == -1 )
								nextspace = line.Length - 1;

							var word = line.Substring( wordindex, nextspace - wordindex + 1 );

							if( column + word.Length < wrap )
							{
								buildline.Append( word );
								column += word.Length;
							}
							else
							{
								base.Write( buildline.ToString() );
								base.Write( NewLine );
								buildline.Length = 0;

								if( word.Length > indentwrap )
								{
									var wi = 0;
									for( ; wi < word.Length; wi += indentwrap )
									{
										buildline.Append( word.Substring( wi, indentwrap ) );
										base.Write( buildline.ToString() );
										base.Write( NewLine );
										buildline.Length = 0;
										base.Write( _indentText );
									}
									buildline.Append( word.Substring( wi ) );
									column = word.Length - wi + _indentText.Length;
								}
								else
								{
									buildline.Append( _indentText );
									buildline.Append( word );
									column = _indentText.Length + word.Length;
								}
							}

							wordindex = nextspace + 1;
						}


						if( hasTrailingNewLine )
						{
							base.Write( buildline.ToString() );
							base.Write( NewLine );
						}
						else
							_wrapBuffer.Append( buildline.ToString() );
					}
				}
			}
		}

		private void ProcessIndent()
		{
			MakeIndentText();
			if( !_indentOnNext ) return;

			_indentOnNext = false;
			var tab = _tab ?? new string( ' ', TabWidth );
			Write( _indentText );
		}

		private void MakeIndentText()
		{
			if( _indentText != null ) return;

			Debug.Assert( TabWidth*_indent < Wrap );
			_indentText = new string( ' ', TabWidth*_indent );
		}

		private void WriteLineInternal( string line )
		{
			base.Write( line );
			base.Write( NewLine );
		}
	}

	public enum TextAlignment
	{
		Left = 0,
		Center = 1,
		Right = 2,
	}
}