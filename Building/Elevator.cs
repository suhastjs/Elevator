using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace Building
{
    public class ElevatorRequest
    {
        public int ElevatorId { get; set; }

        public bool Direction { get; set; } = true;

        public int Floor { get; set; } = 0;
        public bool FromInside { get; set; } = false;
    }

    public enum DoorState
    {
        Open,
        Opening,
        Closed,
        Closing
    }

    public enum ElevatorState
    {
        Stopped,
        Working
    }

    public class Elevator
    {
        public int Id { get; private set; }

        private int floorUpperLimit;
        
        /// <summary>
        /// Gets floor number from where elevator starts
        /// </summary>
        public int FloorUpperLimit { get => floorUpperLimit; set => floorUpperLimit = value; }

        private int floorLowerLimit;

        /// <summary>
        /// Gets floor number at which elevator movement ends
        /// </summary>
        public int FloorLowerLimit
        {
            get => floorLowerLimit;
            set
            {
                if(value >= floorUpperLimit)
                {
                    throw new Exception("Invalid Elevator configuration");
                }

                floorLowerLimit = value;
            }
        }

        /// <summary>
        /// Gets or sets Elevator direction. True => Going UP, False => going down.
        /// </summary>
        public bool Direction { get; set; } = true;

        /// <summary>
        /// Gets or sets current floor
        /// </summary>
        public int CurrentFloor { get; set; }

        /// <summary>
        /// Gets or sets Elevator door status. True => Closed, False => Open.
        /// </summary>
        public DoorState DoorStatus { get; set; } = DoorState.Closed;

        /// <summary>
        /// Gets or sets Elevator door status. True => Closed, False => Open.
        /// </summary>
        public ElevatorState Status { get; set; } = ElevatorState.Stopped;

        // Elevator takes 2 seconds to move to next/previous floor
        private Timer moveToFloorDelay = new Timer()
        {
            Interval = 2000,
            AutoReset = false
        };

        // Elevator takes 1/2 seconds to open the door
        private Timer doorOpeningDelay = new Timer()
        {
            Interval = 500,
            AutoReset = false
        };

        // Elevator Door will be open for 5 seconds
        private Timer doorOpenDelay = new Timer()
        {
            Interval = 5000,
            AutoReset = false
        };

        // Elevator takes 1/2 Second to close the door 
        private Timer doorCloseingDelay = new Timer()
        {
            Interval = 500,
            AutoReset = false
        };

        private bool interruptDoorClose = false;

        public delegate void RequestCompletedDelegate(KeyValuePair<Guid, ElevatorRequest> request);

        /// <summary>
        /// Elevator Event to notify that the request is served for outside requests. 
        /// </summary>
        public event RequestCompletedDelegate RequestCompleted;

        public delegate void NotifyCurrentPosition(int liftId, bool direction, int nextFloor);

        /// <summary>
        /// Elevator Event to notify the builder regarding current position
        /// </summary>
        public event NotifyCurrentPosition NofityCurrentPosition;

        private ConcurrentDictionary<Guid, ElevatorRequest> requests = new ConcurrentDictionary<Guid, ElevatorRequest>();

        /// <summary>
        /// Initializes an object of the class <see cref="Elevator"/>
        /// </summary>
        /// <param name="lowerLimit">Elevator movement starts from which floor</param>
        /// <param name="upperLimit">Elevator movement ends at which floor</param>
        public Elevator(int id, int lowerLimit, int upperLimit)
        {
            this.Id = id;
            this.floorLowerLimit = lowerLimit;
            this.floorUpperLimit = upperLimit;
            this.CurrentFloor = this.floorLowerLimit;
            moveToFloorDelay.Elapsed += MoveToFloorDelay_Elapsed;
            doorOpeningDelay.Elapsed += DoorOpeningDelay_Elapsed;
            doorOpenDelay.Elapsed += DoorOpenDelay_Elapsed;
            doorCloseingDelay.Elapsed += DoorCloseingDelay_Elapsed;
        }

        /// <summary>
        /// Method to signal the elevator that a request has been assigned to it.
        /// </summary>
        /// <param name="id">Request id</param>
        /// <param name="request">Request parameter</param>
        public void SignalFromOutside(Guid id, ElevatorRequest request)
        {
            // Signal door to open if the lift is in same floor and is closing or closed
            if (this.DoorStatus == DoorState.Closed  && this.CurrentFloor == request.Floor)
            {
                // TODO: Signal Door to Open
                doorOpenDelay.Start();
                return;
            }

            request.ElevatorId = this.Id;
            this.requests.TryAdd(id, request);

            if (this.Status == ElevatorState.Stopped)
            {
                Console.WriteLine($"Elevator {Id} Strated because of outside request");
                PerformLiftJob(request.Direction);
            }
        }

        /// <summary>
        /// Signal elevator to move to certain floor of user choice. 
        /// </summary>
        /// <param name="id">Request Id</param>
        /// <param name="request">Request Parameters</param>
        public void SignalFromInside(Guid id, ElevatorRequest request)
        {
            request.FromInside = true;
            if (this.CurrentFloor != request.Floor)
            {
                request.ElevatorId = this.Id;
                this.requests.TryAdd(id, request);
            }

            if (this.Status == ElevatorState.Stopped)
            {
                Console.WriteLine($"Elevator {Id} Strated from inside request");
                if (request.Floor == this.CurrentFloor)
                {
                    DoorOpeningDelay_Elapsed(null, null);
                }
                else
                {
                    request.Direction = this.Direction = request.Floor > this.CurrentFloor;
                    PerformLiftJob(this.Direction);
                }
            }
        }

        /// <summary>
        /// Interrupt door event which will open the lift door if cloosing 
        /// </summary>
        public void InterruptDoorClosing()
        {
            if(this.DoorStatus == DoorState.Closing)
            {
                interruptDoorClose = true;
            }
        }

        /// <summary>
        /// Calculate the request offset so that building can make necessary decision on assigning the request.
        /// </summary>
        /// <param name="requestedFloor"></param>
        /// <returns></returns>
        public int CalculateOffset(int requestedFloor, bool direction)
        {
            var offset = default(int);
            offset = (this.CurrentFloor < requestedFloor) ? requestedFloor - this.CurrentFloor : this.CurrentFloor - requestedFloor;

            if (this.Status == ElevatorState.Working && this.Direction != direction)
            {
                offset += this.FloorUpperLimit;
            }
            
            return offset;
        }

        public void CancelRequest(Guid requestId)
        {
            this.requests.TryRemove(requestId, out ElevatorRequest value);
        }

        private void PerformLiftJob(bool direction)
        {
            this.Status = ElevatorState.Working;

            this.Direction = direction;
            if ((this.Direction && CurrentFloor == floorUpperLimit) || (!this.Direction && CurrentFloor == FloorLowerLimit))
            {
                // Reset Direction if Elevator is on the limits
                this.Direction = !this.Direction;
            }

            this.DoorStatus = DoorState.Closing;
            DoorCloseingDelay_Elapsed(null, null);
        }

        private void DoorCloseingDelay_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (interruptDoorClose)
            {
                // Lock so that no other thread can reset the interrupt Close again as it has no effect
                lock (this)
                {
                    this.DoorStatus = DoorState.Opening;
                    interruptDoorClose = false;
                }

                doorOpeningDelay.Start();
            }
            else
            {
                this.DoorStatus = DoorState.Closed;
                moveToFloorDelay.Start();
            }
        }

        private void MoveToFloorDelay_Elapsed(object sender, ElapsedEventArgs e)
        {            
            // Reverse the direction if the elevator is on lower or upper limit.
            if ((this.Direction && CurrentFloor == floorUpperLimit) || (!this.Direction && CurrentFloor == FloorLowerLimit))
            {
                // Reset Direction if Elevator is on the limits
                this.Direction = !this.Direction;
            }

            if (this.Direction)
            {
                // Going up
                this.CurrentFloor++;
            }
            else
            {
                this.CurrentFloor--;
            }

            NofityCurrentPosition?.Invoke(this.Id, this.Direction, this.CurrentFloor);

            // Check if there is any request to be served for current floor. If not, Move to next floor without door opening. 
            if (this.requests.Where(item => item.Value.Direction == this.Direction && item.Value.Floor == this.CurrentFloor).Count() != 0)
            {
                this.DoorStatus = DoorState.Opening;
                doorOpeningDelay.Start();
            }
            else if(this.requests.Where(item => item.Value.FromInside == true).Count() ==0 && this.requests.Where(item => item.Value.Floor == this.CurrentFloor).Count() != 0)
            {
                // there are no more inside requests from inside. So outside request has to be prioritized.
                this.DoorStatus = DoorState.Opening;
                doorOpeningDelay.Start();
            }
            else
            {
                moveToFloorDelay.Start();
            }
        }

        private void DoorOpeningDelay_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.DoorStatus = DoorState.Open;

            var requestsServed = this.requests.Where(item => item.Value.Floor == CurrentFloor).ToArray();
            var continueLiftOperation = EmptyRequests();
            if(!continueLiftOperation)
            {
                Status = ElevatorState.Stopped;
                moveToFloorDelay.Stop();
                doorOpeningDelay.Stop();
                doorOpenDelay.Stop();
                doorCloseingDelay.Stop();
                Console.WriteLine($"Elevator {Id} Stopped");
            }
            else
            {
                doorOpenDelay.Start();
            }


            foreach (var item in requestsServed)
            {
                RequestCompleted?.Invoke(item);
            }
        }

        private void DoorOpenDelay_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.DoorStatus = DoorState.Closing;
            if (this.Status == ElevatorState.Working)
            {
                doorCloseingDelay.Start();
            }
        }

        private bool EmptyRequests()
        {
            var continueLiftOperation = false;
            // Remove all the current floor requests 
            var requestServed = this.requests.Where(item => item.Value.Floor == CurrentFloor).ToArray();
            foreach (var item in requestServed)
            {
                this.requests.TryRemove(item.Key, out ElevatorRequest request);
            }

            var count = this.requests.Where(item => item.Value.Floor != CurrentFloor).Count();

            if (count != 0)
            {
                continueLiftOperation = true;
            }

            return continueLiftOperation;
        }
    }
}