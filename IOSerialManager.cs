using System;
using System.Management;

using NSFoundation;
using static NSFoundation.Keywords;

using TinyCodeStudio.Headers;


namespace TinyCodeStudio.Classes.IO
{
	public class IOSerialManager : NSObject
	{
		#region Constructors
		public IOSerialManager(IOSerialManagerDelegate @delegate)
		{
			_delegate = @delegate;
			_isDiscovering = NO;
			_discoveryTimerInterval = Defines.kDiscoveryTimerInterval;
			_portsFound = NSMutableArray.array();
			_lock = new NSObject();

		}

		#endregion

		#region Static Methods
		public static IOSerialManager serialManagerWithDelegate(IOSerialManagerDelegate @delegate)
		{
			return new IOSerialManager(@delegate);
		}

		public static NSArray ListPortsWithError(out NSError error)
		{
			NSMutableArray ret = NSMutableArray.array();
            error = null;

            // TODO: A tester.
            try
            {
                ManagementObjectSearcher manObjSearch = new ManagementObjectSearcher("Select * from Win32_SerialPort");
                ManagementObjectCollection manObjReturn = manObjSearch.Get();

                foreach (ManagementObject manObj in manObjReturn)
                {
                    String portName = manObj["Name"].ToString();
                    ret.addObject((NSString)portName);
                }
            }
            catch (Exception ex)
            {
                error = NSError.errorWithException(ex);
            }

            

            return NSArray.arrayWithArray(ret);
		}

		#endregion

		#region Public Properties
		/**
		 Get or set the timer interval for discovering new serial ports.
		 */
		public float DiscoveryTimerInterval { get { return getDiscoveryTimerInterval(); } set { setDiscoveryTimerInterval(value); } }
		#endregion

		#region Private Variables
		IOSerialManagerDelegate/*<IOSerialManagerDelegate>*/ _delegate;
		BOOL _isDiscovering;
		float _discoveryTimerInterval;
		NSTimer _timerDiscovery;  // TODO: either make NSTimer class or use a C# Timer.
		NSMutableArray _portsFound;
		NSObject _lock;
		#endregion

		#region Public Methods
		public void startPortsDiscovery()
		{
			if (_isDiscovering == NO)
			{
				_isDiscovering = YES;
				
                // TODO: remake in C#:
				// Start the timer:
				/*dispatch_queue_t q_background = dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_LOW, 0);
				dispatch_async(q_background, ^{ */
					this._timerDiscovery = NSTimer.scheduledTimerWithTimeInterval(this._discoveryTimerInterval, this, new Selector("discoveryTimer"), null, YES);
					//NSRunLoop runloop = NSRunLoop.currentRunLoop();
					//runloop.addTimer(this._timerDiscovery, NSDefaultRunLoopMode);
					//runloop.run();
				/*});*/
				
				if (_delegate != null && _delegate.respondsToSelector(new Selector("serialManagerDidStartDiscovery", typeof(IOSerialManager), typeof(NSError))) == YES)
				{
					_delegate.serialManagerDidStartDiscovery(this, null);
				}
			}
		}

		public void stopPortsDiscovery()
		{
			if (_isDiscovering == YES)
			{
				_isDiscovering = NO;
				if (_timerDiscovery != null) _timerDiscovery.invalidate();
				
				if (_delegate != null && _delegate.respondsToSelector(new Selector("serialManagerDidStopDiscovery", typeof(IOSerialManager), typeof(NSError))) == YES)
				{
					_delegate.serialManagerDidStopDiscovery(this, null);
				}
			}
		}

		#endregion

		#region Private Methods
		private float getDiscoveryTimerInterval()
		{
			return _discoveryTimerInterval;
		}

		private void setDiscoveryTimerInterval(float discoveryTimerInterval)
		{
			_discoveryTimerInterval = discoveryTimerInterval;
			
			if (_isDiscovering == YES && _timerDiscovery != null)
			{
				this.stopPortsDiscovery();
				this.startPortsDiscovery();
			}
		}

		/**
		 * Method launched by the discovery timer. Each time, the bsd serial ports are retrieved and compared with
		 * the previous iteration. If a port is added, triggers the delegate method portAdded. If a port is removed,
		 * triggers the delegate method portRemoved.
		 */
		private void discoveryTimer()
		{
			NSError error = null;
			lock(_lock)
			{
				NSArray ports = IOSerialManager.ListPortsWithError(out error);
				if (error != null)
				{
					if (_delegate != null && _delegate.respondsToSelector(new Selector("serialManagerDidFindError", typeof(IOSerialManager), typeof(NSError))))
					{
						_delegate.serialManagerDidFindError(this, error);
					}
				}
				else
				{
					// We compare the ports found with the ports we already have:
					foreach (NSString port in ports)
					{
						if (_portsFound.containsObject(port) == NO)
						{
							_portsFound.addObject(port);
							if (_delegate != null && _delegate.respondsToSelector(new Selector("serialManagerPortAdded", typeof(IOSerialManager), typeof(NSString))) == YES)
							{
								_delegate.serialManagerPortAdded(this, port);
							}
						}
					}
					
					//Check ports that have been removed:
					NSMutableArray portsToRemove = NSMutableArray.array();
					foreach (NSString port in _portsFound)
					{
						if (ports != null && ports.containsObject(port) == NO)
						{
							portsToRemove.addObject(port);
							if (_delegate != null && _delegate.respondsToSelector(new Selector("serialManagerPortRemoved", typeof(IOSerialManager), typeof(NSString))) == YES)
							{
								_delegate.serialManagerPortRemoved(this, port);
							}
						}
					}
					
					// Remove ports from the list of ports found:
					_portsFound.removeObjectsInArray(portsToRemove);
				}
			}
		}

		#endregion


	}

	public interface IOSerialManagerDelegate : NSObjectP
	{
        void serialManagerPortAdded(IOSerialManager serialManager, NSString port);
		void serialManagerPortRemoved(IOSerialManager serialManager, NSString port);
        void serialManagerDidFindError(IOSerialManager serialManager, NSError error);
        void serialManagerDidStartDiscovery(IOSerialManager serialManager, NSError error);
        void serialManagerDidStopDiscovery(IOSerialManager serialManager, NSError error);

    }


}
