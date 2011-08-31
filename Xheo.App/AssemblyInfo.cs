// Build number/version template			
using System.Reflection;
#if DIAGNOSTIC
#warning -- DIAGNOSTIC VERSION --
#endif
#if TRIAL
#warning -- TRIAL VERSION --
#endif

[assembly: AssemblyProduct( "XHEO\u00AE Shared Library v5.0" )]
[assembly: AssemblyTitle( "XHEO\u00AE Application Core Framework v5.0" )]

// Version
[assembly: System.Resources.SatelliteContractVersion( "5.0.0.0" )]
#if NET40
	[assembly: AssemblyVersion( "5.0.4000.0" )]
	[assembly: AssemblyFileVersion("5.0.4000.1")]
#else
	#if NET35
		[assembly: AssemblyVersion( "5.0.3500.0" )]
		[assembly: AssemblyFileVersion("5.0.3500.1")]
	#else
		#if NET30
			[assembly: AssemblyVersion( "5.0.3000.0" )]
			[assembly: AssemblyFileVersion("5.0.3000.1")]
		#else
			#if NET20
				[assembly: AssemblyVersion( "5.0.2000.0" )]
				[assembly: AssemblyFileVersion("5.0.2000.1")]
			#else
				#error "Unknown framework version."
			#endif
		#endif
	#endif
#endif
