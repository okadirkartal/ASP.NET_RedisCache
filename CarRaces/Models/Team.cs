using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.SqlServer;

namespace CarRaces.Models
{
    public class Car
    {

        public int ID { get; set; }

        public string Name { get; set; }

        public long Hp { get; set; }

         
        static public void RaceOfCars(IEnumerable<Car> cars)
        {
            Random r = new Random();
            foreach(var element in cars)
            {
                element.Hp = r.Next(1000,3000);
            }
        }
    }


    public class CarContext:DbContext
    {
        public CarContext() : base("CarContext") { }

        public DbSet<Car> Cars { get; set; }
    }


    public class CarInitializer:CreateDatabaseIfNotExists<CarContext>
    {
        protected override void Seed(CarContext context)
        {
            var cars = new List<Car>
            {
                new Car { Name="BMW"},
                new Car{ Name="Mercedes"},
                new Car { Name="Ferrari"},
                new Car{ Name="Lamborghini"},
                new Car { Name="Aston Martin"},
                new Car { Name="Porsche"}
            };

            Car.RaceOfCars(cars);
            cars.ForEach(t=>context.Cars.Add(t));
            context.SaveChanges();
        }
    }



    public class CarConfiguration:DbConfiguration
    {
        public CarConfiguration()
        {
            SetExecutionStrategy("System.Data.SqlClient",()=>new SqlAzureExecutionStrategy());
        }
    }
}