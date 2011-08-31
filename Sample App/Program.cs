using System;
using Xheo.App.v5;

namespace Sample_App
{
	internal class Program : ApplicationInstance
	{
		// Comamnd line arguments
		[Argument(ArgumentOptions.Common | ArgumentOptions.Required, Default = true, ShortName = "", Name = "message",
			ValueName = "message",
			HelpText = "The message to display.")]
		public string Message { get; set; }

		[Argument(ArgumentOptions.Common, ShortName = "e", Name = "exitCode", ValueName = "int",
			HelpText = "The exit code to report.")]
		public int ExitCode { get; set; }

		[Argument( ArgumentOptions.IsCommand, ShortName = "?", Name = "help",
			HelpText = "Shows this help screen." )]
		public bool Help { get; set; }


		public override string ApplicationName
		{
			get { return "Sapmle App"; }
		}

		private static int Main(string[] args)
		{
			return Run(args, new Program());
		}

		protected override int RunInstance(string[] arguments, bool firstInstance)
		{
			if (CommandLine.Parse(this, arguments, null))
			{
				Console.WriteLine(Message);
				if( firstInstance )
				{
					Console.WriteLine("...first instance. Press any key to exit.");
					Console.ReadLine();
				}

				return ExitCode;
			}

			return CommandLine.ShowErrorOrUsage(this, Help);
		}

	}
}