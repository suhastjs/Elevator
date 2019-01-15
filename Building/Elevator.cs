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
    }

    public enum DoorState
    {
        Open,
        Opening,
        Closed,
        Closing
    }

    public enum ElevaterState
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
        public ElevaterState Status { get; set; } = ElevaterState.Stopped;

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

        public delegate void RequestCompletedDelegate(KeyValuePair<Guid, ElevatorRequest>[] request);

        public event RequestCompletedDelegate RequestCompleted;        

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
        
        private ConcurrentDictionary<Guid, ElevatorRequest> requests = new ConcurrentDictionary<Guid, ElevatorRequest>();

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
        }

        public void SignalFromInside(Guid id, ElevatorRequest request)
        {
            if (this.CurrentFloor != request.Floor)
            {
                request.ElevatorId = this.Id;
                this.requests.TryAdd(id, request);
            }

            if (this.Status == ElevaterState.Stopped)
            {
                PerformLiftJob(request.Direction);
            }
        }

        public void InterruptDoorClosing()
        {
            if(this.DoorStatus == DoorState.Closing)
            {
                interruptDoorClose = true;
            }
        }

        public void CancelRequest(Guid requestId)
        {
            this.requests.TryRemove(requestId, out ElevatorRequest value);
        }

        private void PerformLiftJob(bool direction)
        {
            this.Status = ElevaterState.Working;

            this.Direction = direction;
            if ((this.Direction && CurrentFloor == floorUpperLimit) || (!this.Direction && CurrentFloor == FloorLowerLimit))
            {
                // Reset Direction if Elevator is on the limits
                this.Direction = !this.Direction;
            }

            // Assuming that Door status will be closed when first request arive ot when Lift is idle.
            this.DoorStatus = DoorState.Closed;
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

            this.DoorStatus = DoorState.Opening;
            doorOpeningDelay.Start();
        }

        private void DoorOpeningDelay_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.DoorStatus = DoorState.Open;
            var continueLiftOperation = EmptyRequests();
            if(!continueLiftOperation)
            {
                Status = ElevaterState.Stopped;
            }

            doorOpenDelay.Start();
        }

        private void DoorOpenDelay_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.DoorStatus = DoorState.Closing;
            if (this.Status == ElevaterState.Working)
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

            this.requests = (ConcurrentDictionary<Guid, ElevatorRequest>)this.requests.Where(item => item.Value.Floor != CurrentFloor);

            RequestCompleted?.Invoke(requestServed);

            if (this.requests.Count != 0)
            {
                continueLiftOperation = true;
            }

            return continueLiftOperation;
        }
    }
}