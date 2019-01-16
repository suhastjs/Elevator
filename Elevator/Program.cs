using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Building;

namespace Elivator
{
    class Program
    {
        static Building.Building building = null;
        static Random rand = new Random();
        static void Main(string[] args)
        {
            
            building = new Building.Building(10, 4);

            building.ElevaterAssigned += Building_ElevaterAssigned;
            building.ElevaterServed += Building_ElevaterServed;
            building.ElevaterReAssigned += Building_ElevaterReAssigned;

            // The code provided will print ‘Hello World’ to the console.
            // Press Ctrl+F5 (or go to Debug > Start Without Debugging) to run your app.
            for (int i = 0; i < 10; i++)
            {
                bool direction = rand.Next(2) == 1;
                int currentFloor = rand.Next(building.Floors); // currentFloor floor from where user is requesting the elevator

                var request = new ElevatorRequest()
                {
                    Direction = direction,
                    Floor = currentFloor
                };
                building.SignalElivatorFromOutside(request);

                Console.WriteLine($"Elevator requested from {request.Floor} Floor towards direction "+ ((request.Direction) ? "UP" : "Down"));
                Thread.Sleep(5000);
            }

            Console.ReadKey();
        }

        private static void Building_ElevaterAssigned(ElevatorRequest request)
        {
            Console.WriteLine($"Assigned Elevator id {request.ElevatorId}");
        }

        private static void Building_ElevaterReAssigned(ElevatorRequest request)
        {
            Console.WriteLine($"Reassigned Elevator id {request.ElevatorId}");
        }

        private static void Building_ElevaterServed(ElevatorRequest request)
        {
            if (request.FromInside)
            {
                Console.WriteLine($"Elevator Intenally served by Elevator Id: {request.ElevatorId} to {request.Floor} Floor");
            }
            else
            {
                Console.WriteLine($"Elevator served by Elevator Id: {request.ElevatorId}");
                int requestedFloor = rand.Next(building.Floors - 1);
                building.SignalElevatorFromInside(request.ElevatorId, requestedFloor);
                Console.WriteLine($"Elevator requested from {request.ElevatorId} for {requestedFloor} Floor");
            }
        }
    }
}
