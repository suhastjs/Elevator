using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Building
{
    public class Building
    {
        public ConcurrentDictionary<Guid, ElevatorRequest> rootRequests = new ConcurrentDictionary<Guid, ElevatorRequest>();

        private int floors;

        /// <summary>
        /// Gets building floor count. 0 = Lower basement, 1= Upper basement, 2 is Ground floor and so on
        /// </summary>
        public int Floors { get => floors; private set => floors = value; }

        private int elevatorCount;
        
        public int ElevatorCount { get => elevatorCount; private set => elevatorCount = value; }

        private List<Elevator> elevators { get; set; } = new List<Elevator>();

        public delegate void ElevatorAssignedDelegate(ElevatorRequest request);

        public event ElevatorAssignedDelegate ElevaterAssigned;
        public event ElevatorAssignedDelegate ElevaterReAssigned;
        public event ElevatorAssignedDelegate ElevaterServed;

        public Building(int numberOfFloors, int numberOfElevators)
        {
            this.floors = numberOfFloors;
            this.elevatorCount = numberOfElevators;
            for (int i = 0; i < this.elevatorCount; i++)
            {
                var elevator = new Elevator(i, 0, this.Floors);
                elevator.RequestCompleted += Elevator_RequestCompleted;
                elevator.NofityCurrentPosition += Elevator_NofityCurrentPosition;
                elevators.Add(elevator);
            }
        }

        public void AddAFloor()
        {
            this.floors += 1;
        }

        public void AddAnElivator()
        {
            this.elevatorCount += 1;
            var elevator = new Elevator(this.elevatorCount, 0, this.Floors);
            elevator.RequestCompleted += Elevator_RequestCompleted;
            elevator.NofityCurrentPosition += Elevator_NofityCurrentPosition;
            elevators.Add(elevator);
        }

        private void Elevator_NofityCurrentPosition(int liftId, bool direction, int currentFloor)
        {
            var elevator = elevators.Where(item => item.Id == liftId).FirstOrDefault();
            var requestMatches = rootRequests.Where(item => !item.Value.ElevatorId.Equals(liftId) && item.Value.Direction.Equals(direction) && item.Value.Floor.Equals(currentFloor + 1)).ToList();
            foreach (var match in requestMatches)
            {
                var elevatorCurrent = elevators.Where(item => item.Id == liftId).FirstOrDefault();
                elevatorCurrent?.CancelRequest(match.Key);
                match.Value.ElevatorId = liftId;
                elevator?.SignalFromOutside(match.Key, match.Value);
                ElevaterReAssigned?.Invoke(match.Value);
            }
        }

        private void Elevator_RequestCompleted(KeyValuePair<Guid, ElevatorRequest> request)
        {
            rootRequests.TryRemove(request.Key, out ElevatorRequest data);
            ElevaterServed?.Invoke(request.Value);
        }

        /// <summary>
        /// Method to adjust the lift behavior like restricting the lift to toggle between limits. 
        /// </summary>
        /// <param name="elevatorId">Elevator ID</param>
        /// <param name="lowerLimit">Elevator lower floor limit</param>
        /// <param name="upperLimit">Elevator upper floor limit</param>
        public void AdjustFloor(int elevatorId, int lowerLimit, int upperLimit)
        {
            var elevetor = elevators.Where(item => item.Id == elevatorId).FirstOrDefault();
            elevetor.FloorUpperLimit = upperLimit;
            elevetor.FloorLowerLimit = lowerLimit;
        }

        public void SignalElivatorFromOutside(ElevatorRequest request)
        {
            rootRequests.TryAdd(Guid.NewGuid(), request);
            AssignElevator(request);
        }

        public void SignalElevatorFromInside(int elevatorId, int requestedFloor)
        {
            var elevetor = elevators.Where(item => item.Id == elevatorId).FirstOrDefault();
            elevetor?.SignalFromInside(Guid.NewGuid(), new ElevatorRequest()
            {
                Direction = elevetor.Direction, // This just for assignment purpose
                ElevatorId = elevatorId,
                Floor = requestedFloor
            });
        }

        private void AssignElevator(ElevatorRequest request)
        {
            int offset = int.MaxValue;
            int elevatorId = 0;
            Elevator elevator = null;
            foreach (var item in elevators)
            {
                int currentOffset = item.CalculateOffset(request.Floor, request.Direction);
                if(currentOffset < offset)
                {
                    elevator = item;
                    request.ElevatorId = elevatorId = item.Id;
                    offset = currentOffset;
                }
            }

            if(elevator!= null && elevator.Status == ElevatorState.Stopped)
            {
                elevator.Direction = request.Direction;
            }

            elevator?.SignalFromOutside(Guid.NewGuid(), request);
            ElevaterAssigned?.Invoke(request);
        }
    }
}
