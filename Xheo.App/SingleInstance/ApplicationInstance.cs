using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace Xheo.App.v5
{
	/// <summary>
	/// Implements a basic framework for a single instance application.
	/// </summary>
	/// <example>
	/// public class App : ApplicationInstance
	/// {
	///		public static int Main( string[] args )
	///		{
	///			return Run( args, new App() );
	///		}
	///		
	///		public override string ApplicationName { get{ return "MyUniqueAppName"; } }
	/// 
	///		protected override int RunInstance( string[] args, bool newInstance )
	///		{
	///			if( ! newInstance )
	///				// Do something, it's ok to wait for user response
	///			else
	///				// Parse a second request to start the application
	/// 
	///         // Typically 0 is success, non-zero is error.
	///			return 0;
	///		}
	/// }
	/// </example>
	public abstract class ApplicationInstance : MarshalByRefObject
	{
		private static ApplicationInstance _instance;
		private Mutex _mutex;
		private string _mutexName;
		private ManualResetEvent _stopEvent;
		private Thread _ipcThread;

		public static TimeSpan MarshalTimeout = 
#if DEBUG
			TimeSpan.FromSeconds( 3 );
#else
			TimeSpan.FromSeconds( 30000 );
#endif

		/// <summary>
		/// Gets a reference to the current <see cref="ApplicationInstance"/> for the process.
		/// </summary>
		public static ApplicationInstance Instance { get { return _instance; } }

		/// <summary>
		/// Gets a value that indicates if the application should be a single isntance.
		/// </summary>
		public virtual bool SingleInstance { get { return true; } }

		/// <summary>
		/// Gets the name of the application. Used to uniquely identify running applications.
		/// </summary>
		public abstract string ApplicationName { get; }

		///	<summary>
		///	Fired when the last instance is shut down.
		/// </summary>
		public event EventHandler ShutDown;

		/// <summary>
		///	Raises the <see cref="ShutDown" /> event.
		/// </summary>
		protected internal virtual void OnShutDown( EventArgs e )
		{
			if( ShutDown != null )
				ShutDown( this, e );
		}

		///	<summary>
		///	Fired when the main form of an application is closed.
		/// </summary>
		public event EventHandler Closed;

		/// <summary>
		///	Raises the <see cref="Closed" /> event.
		/// </summary>
		protected internal virtual void OnClosed( EventArgs e )
		{
			if( Closed != null )
				Closed( this, e );
		}

		/// <summary>
		/// Runs the application and manages any IPC communications.
		/// </summary>
		/// <param name="arguments">
		///		The startup arguments passed into the application. If null loads the default
		///		values from the command line.
		/// </param>
		/// <param name="instance">
		///		The application instance to control. If <see cref="ApplicationInstance.SingleInstance"/> 
		///		is true this instance will either become the single instance, or will be destroyed 
		///		and the original startup commands will be passed to the existing instance.
		/// </param>
		/// <returns></returns>
		public static int Run( string[] arguments, ApplicationInstance instance )
		{
			if( instance == null )
				throw new ArgumentNullException( "instance" );

			Debug.Assert( _instance == null, "There should only be one instance of an ApplicationInstance object per process." );
			_instance = instance;

			if( arguments == null )
				arguments = Environment.GetCommandLineArgs();

			arguments = instance.PreProcessArguments( arguments );

			return instance.SingleInstance
			       	? instance.RunAsSingleInstance( arguments )
			       	: instance.RunInstanceWithEvents( arguments, true );
		}

		/// <summary>
		/// Gives the application to an oportunity to process the arguments before the
		/// single instance framework starts the application. Can be used to provide a 'multiple instance'
		/// command line argument or fix any file paths relative to the second instance 
		/// working folder.
		/// </summary>
		/// <param name="arguments">The command line arguments.</param>
		/// <returns>Returns the adjusted command line agruments.</returns>
		protected virtual string[] PreProcessArguments( string[] arguments ) { return arguments; }

		/// <summary>
		/// Called by the <see cref="ApplicationInstance"/> when the application should
		/// begin processing, or enter it's message loop. May be called on a different
		/// thread.
		/// </summary>
		/// <param name="arguments">
		///		The arguments passed on the command line.
		/// </param>
		/// <param name="firstInstance">
		///		Indicates if this the first instance of the application. When false, the
		///		<paramref name="arguments"/> parameter holds values propagated from the
		///		secondary instance that tried to run.
		/// </param>
		/// <remarks>
		///		Returns the process exit.
		/// </remarks>
		protected internal abstract int RunInstance( string[] arguments, bool firstInstance );

		/// <summary>
		/// Runs the application like a normal process and ignores any existing running instances.
		/// </summary>
		/// <param name="arguments">The command line arguments</param>
		///<param name="firstInstance">Indicates if the instance is the first intance.</param>
		///<returns>
		///		Returns the process exit code.
		/// </returns>
		private int RunInstanceWithEvents( string[] arguments, bool firstInstance )
		{
			var result = 0;

			try
			{
				result = RunInstance( arguments, firstInstance );
			}
			catch
			{
				result = -1;
			}
			finally
			{
				OnClosed( EventArgs.Empty );
				OnShutDown( EventArgs.Empty );
			}

			return result;
		}

		/// <summary>
		/// Runs the application as a single instance app. When a second instance attempts to run, the command
		/// line arguments will be propagated to the original host application and the original instance
		/// will be <see cref="Run"/> again with <paramref name="firstInstance"/> false.
		/// </summary>
		/// <param name="arguments">The command line arguments</param>
		/// <param name="instance">The ApplicationInstance to run.</param>
		/// <returns>
		///		Returns the process exit code from the first instance.
		/// </returns>
		private int RunAsSingleInstance( string[] arguments )
		{
			if( _mutex != null )
				throw new InvalidOperationException( "Application already started." );

			var result = 0;

			// On second instance open named pipe, send arguments, wait for return code, exit.
			if( ! TakeMutex() )
			{
				if( MarshalCommandLineToMainInstance( arguments, out result ) )
					return result;

				// Couldn't connect or marshal to the other process. Wait a few seconds
				// to see if the other app is closing and try again. If that
				Thread.Sleep( TimeSpan.FromSeconds( 10 ) );

				if( MarshalCommandLineToMainInstance( arguments, out result ) )
					return result;

				
				// Try the mutext again to see if we can become the new primary instance
				if( ! TakeMutex() )
				{
					// Can't connect, can't take over mutex. Just run as a second instance
					return RunInstanceWithEvents( arguments, true );
				}
			}

			try
			{
				// If first instance, setup a new thread and open mutex and named pipe. Run normally.
				try
				{
					ListenForIpcRequests();
					result = RunInstanceWithEvents( arguments, true );
				}
				finally
				{
					StopListeningForIpcRequests();
				}
			}
			finally
			{
				_mutex.ReleaseMutex();
			}

			return result;
		}

		private void StopListeningForIpcRequests()
		{
			_stopEvent.Set();
			_ipcThread.Join( MarshalTimeout );
			if( _ipcThread.IsAlive )
				_ipcThread.Abort();
		}

		private void ListenForIpcRequests() 
		{ 
			_stopEvent = new ManualResetEvent( false );
			_ipcThread = new Thread( IpcThreadMethod )
			{
				IsBackground = true,
				Name = "Single Instance IPC Monitor",
				Priority = ThreadPriority.BelowNormal
			};

			_ipcThread.Start();
		}

		
		/// <summary>
		/// Marshals a second instance to the primary instance.
		/// </summary>
		/// <param name="arguments">The new command line arguments.</param>
		/// <param name="result">[Out] The process exit code returned by the primary instance.</param>
		/// <returns>Returns true if the arguments were marshalled, otherwise false.</returns>
		private bool MarshalCommandLineToMainInstance( string[] arguments, out int result )
		{
			result = -1;
			var retries = 0;
			while( retries < 3 )
			{
				using( var pipe = new NamedPipeClientStream( _mutexName ) )
				{
					try
					{
						pipe.Connect( (int)MarshalTimeout.TotalSeconds );
					}
					catch( TimeoutException )
					{
						return false;
					}
					catch( IOException )
					{
						// The pipe is alive, but busy serving another client, just try again.
						retries++;
						continue;
					}

					var formatter = new BinaryFormatter();
					formatter.Serialize( pipe, arguments );
					pipe.Flush();

					pipe.WaitForPipeDrain();
					var resultBytes = new byte[4];
					pipe.Read( resultBytes, 0, 4 );

					result = BitConverter.ToInt32( resultBytes, 0 );
					return true;
				}
			}

			// Failed to connect, timed out, etc.
			return false;
		}

		private void IpcThreadMethod()
		{
			while( ! _stopEvent.WaitOne( 0 ) )
			{
				using(
					var pipe = new NamedPipeServerStream( _mutexName,
					                                      PipeDirection.InOut,
					                                      1,
					                                      PipeTransmissionMode.Byte,
					                                      PipeOptions.Asynchronous ) )
				{
					var async = pipe.BeginWaitForConnection( null, null );

					switch( WaitHandle.WaitAny( new[] {_stopEvent, async.AsyncWaitHandle} ) )
					{
						case 0: // Received stop request
							continue;

						case 1: // Recieved pip connection
							break;
					}

					pipe.EndWaitForConnection( async );

					var formatter = new BinaryFormatter();
					var arguments = formatter.Deserialize( pipe ) as string[];

					var result = RunInstanceWithEvents( arguments, false );

					pipe.Write( BitConverter.GetBytes( result ), 0, 4 );
					pipe.Flush();
					pipe.WaitForPipeDrain();
				}

			}
		}


		private bool TakeMutex() { return this.TakeMutex( true ); }

		private bool TakeMutex( bool recreateAbandonedMutex )
		{
			var createdMutex = false;
			var doesNotExist = false;
			_mutexName = "Xheo.App.SingleInstanceMutext___" + ApplicationName;

			try
			{
				_mutex = Mutex.OpenExisting( _mutexName, MutexRights.Synchronize );
			}
			catch( WaitHandleCannotBeOpenedException )
			{
				doesNotExist = true;
			}

			if( doesNotExist )
			{
				var mutexSec = new MutexSecurity();

				// Grant everyone rights to synchronize on this mutex
				mutexSec.AddAccessRule( new MutexAccessRule( new SecurityIdentifier( WellKnownSidType.WorldSid, null ),
				                                             MutexRights.Synchronize | MutexRights.Modify,
				                                             AccessControlType.Allow ) );

				_mutex = new Mutex( false, _mutexName, out createdMutex, mutexSec );

				if( ! createdMutex )
					throw new ApplicationException( "Could not create single instance mutex." );
			}

			try
			{
				return _mutex.WaitOne( 0 );
			}
			catch( AbandonedMutexException )
			{
#if DEBUG
				throw;
#else
				if( recreateAbandonedMutex )
					return OpenMutex( false );
				else
					throw;
#endif
			}
		}

	}
}