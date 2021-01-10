using System;
//using System.IO;
using System.IO.Ports;
using System.Threading;


using NSFoundation;
using static NSFoundation.Keywords;

using TinyCodeStudio.Classes.Utils;


namespace TinyCodeStudio.Classes.IO
{
	public enum IOSerialBitsLength
{
		IOSerialBitsLength7Bits,
		IOSerialBitsLength8Bits
	}

	public enum IOSerialParity
{
		IOSerialParityNone,
		IOSerialParityEven,
		IOSerialParityOdd
	}

	public class IOSerial : NSObject
	{
		#region Constructors
		public IOSerial(NSString portName, int speed, IOSerialDelegate/*<IOSerialDelegate>*/ @delegate) : base()
		{
			this.configureWithPortName(portName, speed, IOSerialBitsLength.IOSerialBitsLength8Bits, IOSerialParity.IOSerialParityNone, @delegate);
		}

		public IOSerial(NSString portName, int speed, IOSerialBitsLength bitsLength, IOSerialParity parity, IOSerialDelegate/*<IOSerialDelegate>*/ @delegate) : base()
		{
			this.configureWithPortName(portName, speed, bitsLength, parity, @delegate);

		}

		#endregion

		#region Static Methods
		public static IOSerial serialWithPortName(NSString portName, int speed, IOSerialDelegate/*<IOSerialDelegate>*/ @delegate)
		{
			return new IOSerial(portName, speed, @delegate);
		}

		public static IOSerial serialWithPortName(NSString portName, int speed, IOSerialBitsLength bitsLength, IOSerialParity parity, IOSerialDelegate/*<IOSerialDelegate>*/ @delegate)
		{
			return new IOSerial(portName, speed, (IOSerialBitsLength)bitsLength, (IOSerialParity)parity, @delegate);
		}

		#endregion

		#region Public Properties
		public BOOL IsConnected { get { return getIsConnected(); } }
		public NSString PortName { get { return getPortName(); } set { setPortName(value); } }
		public int Speed { get { return getSpeed(); } set { setSpeed(value); } }
		public IOSerialParity Parity { get { return getParity(); } set { setParity(value); } }
		public IOSerialBitsLength BitsLength { get { return getBitsLength(); } set { setBitsLength(value); } }
		#endregion

		#region Private Variables
		IOSerialDelegate/*<IOSerialDelegate>*/ _delegate;
		int _fileDescriptor;
		BOOL _stopReading;
		int _speed;
		NSString _bsdPortName;
		IOSerialBitsLength _bitsLength;
		IOSerialParity _parity;
		NSObject _lockRead;
		NSObject _lockWrite;

        SerialPort _serialPort;
		#endregion

		#region Public Methods
		public void connect()
		{
			if (this.IsConnected == NO)
			{
                Thread t = new Thread(() => this.connectOnBackground()) { IsBackground = true};
                t.Start();
				/*dispatch_queue_t q_background = dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_LOW, 0);
				dispatch_async(q_background, ^{
					this.connectOnBackground();
				});*/
			}
		}

		public void close()
		{
			if (this.IsConnected == YES)
			{

                Thread t = new Thread(() => {
                    try
                    {
                        if (_serialPort.IsOpen == true)
                        {
                            _serialPort.Close();

                            if (this._delegate != null)
                            {
                                this._delegate.serialDidCloseConnection(this, _bsdPortName.copy());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (this._delegate != null)
                        {
                            this._delegate.serialDidFindError(this, NSError.errorWithException(ex));
                        }
                    }
                }) { IsBackground = true };
                t.Start();
                /*dispatch_queue_t q_background = dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_LOW, 0);
				dispatch_async(q_background, ^{
					NSError error = null;
					this._stopReading = YES;
					
					// Block until all written output has been sent from the device.
					// Note that this call is simply passed on to the serial device driver.
					// See tcsendbreak(3) ("man 3 tcsendbreak") for details.
					if (tcdrain(this._fileDescriptor) == -1)
					{
						error = [NSErrorHelper errorWithDescription:"Error waiting for drain" reason:[NSString stringWithFormat:"Error waiting for drain on %@ - %s(%d).", this._bsdPortName, strerror(errno), errno]];
					}
					
					// It is good practice to reset a serial port back to the state in
					// which you found it. This is why we saved the original termios struct
					// The constant TCSANOW (defined in termios.h) indicates that
					// the change should take effect immediately.
					if (tcsetattr(this._fileDescriptor, TCSANOW, &gOriginalTTYAttrs) == -1)
					{
						error = [NSErrorHelper errorWithDescription:"Error resetting tty attributes" reason:[NSString stringWithFormat:"Error resetting tty attributes on %@ - %s(%d).", this._bsdPortName, strerror(errno), errno]];
					}
					
					close(this._fileDescriptor);
					this._fileDescriptor = -1;
					
					if (error != null && this._delegate != null && [this._delegate respondsToSelector:@selector(serial:didFindError:))
					{
						[this._delegate serial:this didFindError:error];
					}
					
					if (this._delegate != null && [this._delegate respondsToSelector:@selector(serial:didCloseConnection:))
					{
						[this._delegate serial:this didCloseConnection:[this._bsdPortName copy]];
					}
				});*/
			}
        }

		public void write(NSData data)
		{
			if (this.IsConnected)
			{
                Thread t = new Thread(() => {
                    // TODO: Write serial data.
                    try
                    {
                        lock (_lockWrite)
                        {
                            if (data != null)
                            {
                                byte[] buffer = data.bytes;
                                if (_serialPort.IsOpen == true)
                                {
                                    _serialPort.Write(buffer, 0, buffer.Length);

                                    if (_delegate != null)
                                    {
                                        _delegate.serialDidWriteData(this, data);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_delegate != null)
                        {
                            _delegate.serialDidFindError(this, NSError.errorWithException(ex));
                        }
                    }
                })
                { IsBackground = true };
                t.Start();
                /*dispatch_queue_t q_background = dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_LOW, 0);
				dispatch_async(q_background, ^{
					NSError error = null;
					@synchronized(this._lockWrite)
					{
						long nPosition = 0;
						do {
							long nWritten = write(this._fileDescriptor, data.bytes+nPosition, data.length - nPosition);
							if (nWritten < 0)
							{
								error = [NSErrorHelper errorWithDescription:"Error while writing data" reason:[NSString stringWithFormat:"Error while writing data on %@ - %s(%d).", this._bsdPortName, strerror(errno), errno]];
								break;
							}
							else
							{
								if (this._delegate != null && [this._delegate respondsToSelector:@selector(serial:didWriteData:))
								{
									NSData tmpData = [data subdataWithRange:NSMakeRange(nPosition, nWritten);
									if (tmpData != null && tmpData.length > 0)
									{
										[this._delegate serial:this didWriteData:tmpData];
									}
								}
								nPosition += nWritten;
							}
						} while (nPosition < data.length);
						
					}
				});*/
            }
		}

		#endregion

		#region Private Methods
		private IOSerialBitsLength getBitsLength()
		{
			return _bitsLength;
		}

		private void setBitsLength(IOSerialBitsLength bitsLength)
		{
			_bitsLength = bitsLength;
		}

		private BOOL getIsConnected()
		{
			return (_fileDescriptor > -1);
		}

		private IOSerialParity getParity()
		{
			return _parity;
		}

		private void setParity(IOSerialParity parity)
		{
			_parity = parity;
		}

		private NSString getPortName()
		{
			return _bsdPortName.copy();
		}

		private void setPortName(NSString portName)
		{
			_bsdPortName = (portName == null) ? (NSString)"" : portName.copy();
		}

		private int getSpeed()
		{
			return _speed;
		}

		private void setSpeed(int speed)
		{
			_speed = speed;
		}

		private void configureWithPortName(NSString portName, int speed, IOSerialBitsLength bitsLength, IOSerialParity parity, IOSerialDelegate/*<IOSerialDelegate>*/ @delegate)
		{
			_speed = speed;
			_bsdPortName = (portName == null) ? (NSString)"" : portName.copy();
			_bitsLength = bitsLength;
			_parity = parity;
			_delegate = @delegate;
			
			_fileDescriptor = -1;
			
			_lockRead = new NSObject();
			_lockWrite = new NSObject();
		}

		private void connectOnBackground()
		{
			NSError error = null;

            Parity parity = System.IO.Ports.Parity.None;
            if (_parity == IOSerialParity.IOSerialParityEven)
            {
                parity = System.IO.Ports.Parity.Even;
            }
            else if (_parity == IOSerialParity.IOSerialParityOdd)
            {
                parity = System.IO.Ports.Parity.Odd;
            }

            try
            {
                _serialPort = new SerialPort(_bsdPortName, _speed, parity, _bitsLength == IOSerialBitsLength.IOSerialBitsLength7Bits ? 7 : 8, StopBits.None);
                _serialPort.ReadTimeout = 500;
                _serialPort.WriteTimeout = 500;

                _serialPort.Open();
                _serialPort.DataReceived += this.read;
            }
            catch (Exception ex)
            {
                error = NSError.errorWithException(ex);
            }

           


			
			// Managing the error:
			if (error != null)
			{
                // TODO: close.
				//if (_fileDescriptor != -1) close(_fileDescriptor);
		
				// Raise error event for the delegate:
				if (_delegate != null && _delegate.respondsToSelector(new Selector("serialDidFindError", typeof(IOSerial), typeof(NSError))))
				{
					_delegate.serialDidFindError(this, error);
				}
			}
		}

		/**
		 * Read data from the serial port continuously
		 * (to be asynchronous this method must be called on a background thread).
		 */
		private void read(object sender, SerialDataReceivedEventArgs args)
		{
            string strData = _serialPort.ReadExisting();
            if (string.IsNullOrEmpty(strData) == false)
            {
                lock (_lockRead)
                {
                    if (_delegate != null)
                    {
                        NSData data = ((NSString)strData).dataUsingEncoding(NSStringEncoding.NSUTF8StringEncoding); // (buffer, nRead);
                        if (data != null)
                        {
                            _delegate.serialDidReadData(this, data);
                        }
                    }
                }
            }
			
		}

        private void error_received(object sender, SerialErrorReceivedEventArgs args)
        {
            if (_delegate != null)
            {
                NSError error = NSErrorHelper.errorWithDescription("Serial error: unknown error.", "");
                SerialError errorValue = args.EventType;
                switch (errorValue)
                {
                    case SerialError.TXFull:
                        error = NSErrorHelper.errorWithDescription("Serial error: TX full.", "");
                        break;
                    case SerialError.RXOver:
                        error = NSErrorHelper.errorWithDescription("Serial error: RX over.", "");
                        break;
                    case SerialError.Overrun:
                        error = NSErrorHelper.errorWithDescription("Serial error: buffer overrun.", "");
                        break;
                    case SerialError.RXParity:
                        error = NSErrorHelper.errorWithDescription("Serial error: RX parity.", "");
                        break;
                    case SerialError.Frame:
                        error = NSErrorHelper.errorWithDescription("Serial error: frame error.", "");
                        break;
                    default:
                        break;
                }
                _delegate.serialDidFindError(this, error);
            }
        }

		#endregion


	}

	public interface IOSerialDelegate : NSObjectP
	{
        void serialDidCloseConnection(IOSerial serial, NSString port);
        void serialDidConnect(IOSerial serial, NSString port);
    
        void serialDataAvailable(IOSerial serial, int dataLength);
        void serialDidFindError(IOSerial serial, NSError error);
        void serialDidReadData(IOSerial serial, NSData data);
        void serialDidWriteData(IOSerial serial, NSData data);

    }

}
