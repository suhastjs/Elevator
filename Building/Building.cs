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

        private List<Elevator> elevators { get; set; }

        public Building(int floors, int elevators)
        {
            this.floors = floors;
            this.elevatorCount = elevators;
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
            elevators.Add(elevator);
        }

        private void Elevator_RequestCompleted(KeyValuePair<Guid, ElevatorRequest>[] request)
        {
            Array.ForEach(request, item => {
                rootRequests.TryRemove(item.Key, out ElevatorRequest data);
            });
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

        private void AssignElevator(ElevatorRequest request)
        {
            //int offset = 0;
            //int elevatorId = 0;
            
            

        }
    }
}
