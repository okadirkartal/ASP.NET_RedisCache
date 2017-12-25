using CarRaces.Models;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;

using StackExchange.Redis;
using System;
using System.Configuration;

using System.Diagnostics;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace CarRaces.Controllers
{
    public class CarsController : Controller
    {
        private CarContext db = new CarContext();

        //Redis ConnectionString info
        private static Lazy<ConnectionMultiplexer> lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            string cacheConnection = ConfigurationManager.AppSettings["CacheConnection"].ToString();
            return ConnectionMultiplexer.Connect(cacheConnection);
        });

        public static ConnectionMultiplexer Connection => lazyConnection.Value;

        // GET: Cars
        public ActionResult Index(string actionType,string resultType)
        {

            List<Car> cars = new List<Car>();
            
            switch(actionType)
            {
                case "raceCars":
                    RaceCars();
                    break;

                case "clearCache":
                    ClearCachedItems();
                    break;

                case "rebuildDB":
                    RebuildDB();
                    break;
            }

            Stopwatch sw = Stopwatch.StartNew();
            switch(resultType)
            {
                case "carsSortedSet":
                    cars = GetFromSortedSet();
                    break;
                case "carsSortedSetTop5":
                    cars = GetFromSortedSetTop5();
                    break;
                case "carsList":
                    cars = GetFromList();
                    break;
                case "fromDB":
                    cars = GetFromDB();
                    break;
            }

            sw.Stop();
            double ms=sw.ElapsedTicks/(Stopwatch.Frequency/(1000.0));

            ViewBag.msg += " MS : " + ms.ToString();

            return View(cars);
        }

        private List<Car> GetFromDB()
        {
            ViewBag.msg += "Results read from DB. ";
            var results = from t in db.Cars orderby t.Hp descending select t;
            return results.ToList<Car>();
        }

        private List<Car> GetFromList()
        {
            List<Car> cars = null;

            IDatabase cache = Connection.GetDatabase();
            string serializedCars = cache.StringGet(Constants.CarsListCacheName);
            if(!String.IsNullOrEmpty(serializedCars))
            {
                cars = JsonConvert.DeserializeObject<List<Car>>(serializedCars);
                ViewBag.msg += "List read from cache. ";
            }
            else
            {
                ViewBag.msg += " Cars list cache miss.";
                cars = GetFromDB();

                ViewBag.msg += " Storing results to cache";
                cache.StringSet(Constants.CarsListCacheName,JsonConvert.SerializeObject(cars));
            }

            return cars;
        }

        private List<Car> GetFromSortedSetTop5()
        {
            List<Car> cars = null;

            IDatabase cache = Connection.GetDatabase();

            var carsSortedSet = cache.SortedSetRangeByRankWithScores(Constants.SortedCarListCacheName, stop: 4, order: Order.Descending);

            if(carsSortedSet.Count()==0)
            {
                GetFromSortedSet();

                carsSortedSet = cache.SortedSetRangeByScoreWithScores(Constants.SortedCarListCacheName,stop:4,order:Order.Descending);
            }

            ViewBag.msg += " Retrieving top 5 teams from cache . ";

            cars = new List<Car>();

            foreach(var carItem in carsSortedSet)
            {
                cars.Add(JsonConvert.DeserializeObject<Car>(carItem.Element));
            }

            return cars;
        }

        private List<Car> GetFromSortedSet()
        {
            List<Car> cars = null;

            IDatabase cache = Connection.GetDatabase();

            var carSortedSet = cache.SortedSetRangeByRankWithScores(Constants.SortedCarListCacheName, order: Order.Descending);

            if(carSortedSet.Count()>0)
            {
                ViewBag.msg += "Reading sorted set from cache. ";
                cars = new List<Car>();

                foreach(var element in carSortedSet)
                {
                    Car carItem = JsonConvert.DeserializeObject<Car>(element.Element);
                    cars.Add(carItem);
                }
            }
             else
            {
                ViewBag.msg += "Cars sorted set cache miss. ";

                cars = GetFromDB();

                ViewBag.msg += "Storing results to cache. ";

                foreach(var element in cars)
                {
                    Console.WriteLine($"Adding to sorted set : {element.Name} - {element.Hp}");
                    cache.SortedSetAdd(Constants.SortedCarListCacheName, JsonConvert.SerializeObject(element), element.Hp);
                } 
            }
            return cars;
        }

        private void RebuildDB()
        {
            ViewBag.msg += "Rebuilding DB. ";
            db.Database.Delete();
            db.Database.Initialize(true);

            ClearCachedItems();
        }

        private void ClearCachedItems()
        {
            IDatabase cache = Connection.GetDatabase();
            cache.KeyDelete(Constants.CarsListCacheName);
            cache.KeyDelete(Constants.SortedCarListCacheName);
            ViewBag.msg += " Car data removed from cache";
        }

        private void RaceCars()
        {
            ViewBag.msg += " Updating car statistics...";
            var cars = from t in db.Cars select t;
            Car.RaceOfCars(cars);
            db.SaveChanges();
            ClearCachedItems();
        }



        // GET: Cars/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Car car = db.Cars.Find(id);
            if (car == null)
            {
                return HttpNotFound();
            }
            return View(car);
        }

        // GET: Cars/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Cars/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "ID,Name,Hp")] Car car)
        {
            if (ModelState.IsValid)
            {
                db.Cars.Add(car);
                db.SaveChanges();

                ClearCachedItems();

                return RedirectToAction("Index");
            }

            return View(car);
        }

        // GET: Cars/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Car car = db.Cars.Find(id);
            if (car == null)
            {
                return HttpNotFound();
            }
            return View(car);
        }

        // POST: Cars/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ID,Name,Hp")] Car car)
        {
            if (ModelState.IsValid)
            {
                db.Entry(car).State = EntityState.Modified;
                db.SaveChanges();

                ClearCachedItems();

                return RedirectToAction("Index");
            }
            return View(car);
        }

        // GET: Cars/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Car car = db.Cars.Find(id);
            if (car == null)
            {
                return HttpNotFound();
            }
            return View(car);
        }

        // POST: Cars/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Car car = db.Cars.Find(id);
            db.Cars.Remove(car);
            db.SaveChanges();

            ClearCachedItems();

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
