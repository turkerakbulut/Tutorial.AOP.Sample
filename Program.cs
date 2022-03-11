using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Text;
using System.Threading;

namespace Tutorial.AOP.Sample
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            /*
             * Create repository proxy
             */
            IRepository repository = Proxy<Repository, IRepository>.GenerateProxy();

            /* 
             * Add new entity
             */
            repository.Add( "Arthur", "Schopenhauer" );

            Console.WriteLine("----------------------------------------");

            /*
             * Add new entity
             */
            repository.Add("Gustave","Le bon" );

            Console.WriteLine("----------------------------------------");

            /*
             * Reindex DB etc.
             * "It returns nothing and takes no arguments"
             */
            repository.ReIndex();

            /*
             * Stop
             */
            Console.ReadLine();
        }
    }

    /// <summary>
    /// Entity class
    /// </summary>
    internal class Entity
    {
        public int ID { get; set; }

        public string Name { get; set; }

        public string LastName { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    /// <summary>
    /// In memory DB class
    /// </summary>
    internal class DB
    {
        private static List<Entity> entities;

        internal static List<Entity> Entities
        {
            get
            {
                if (entities==null)
                {
                    entities= new List<Entity>();
                }
                return entities;
            }
        }
    }

    /// <summary>
    /// Repository class
    /// </summary>
    internal class Repository : IRepository
    {
        
        [PerformanceCounter]
        [Log]
        public Entity Add(string name, string lastName)
        {

            Entity entity = new Entity()
            {
                ID = DB.Entities.Count+1,
                Name = name,
                LastName = lastName
            };

            DB.Entities.Add(entity);
            return entity;
        }

        [PerformanceCounter]
        [Log]
        public bool Delete(int id)
        {
            if (DB.Entities[id]!=null)
            {
                DB.Entities.RemoveAt(id);
                return true;
            }
            else
            {
                return false;
            }
           
        }

        [PerformanceCounter]
        [Log]
        public IList<Entity> GetAll()
        {
            return DB.Entities;
        }

        [PerformanceCounter]
        [Log]
        public void ReIndex()
        {
            Thread.Sleep(200);
        }

        [PerformanceCounter]
        [Log]
        public void Update(int id, string name, string lastName)
        {
            Entity entity = DB.Entities[id];
            if (entity!=null)
                Delete(id);
            Add(name, lastName);
        }
    }

    /// <summary>
    /// Repository interface
    /// </summary>
    internal interface IRepository
    {
        void ReIndex();

        Entity Add(string name, string lastName);

        void Update(int id, string name, string lastName);

        bool Delete(int id);

        IList<Entity> GetAll();
    }

    /// <summary>
    /// Proxy class inherit RealProxy
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TI"></typeparam>
    public class Proxy<T, TI> : RealProxy where T : TI, new()
    {
        public Proxy() : base(typeof(TI))
        {
        }

        /// <summary>
        /// Returns the transparent proxy for the current instance of System.Runtime.Remoting.Proxies.RealProxy
        /// </summary>
        /// <returns></returns>
        public static TI GenerateProxy()
        {
            var instance = new Proxy<T, TI>();
            return (TI)instance.GetTransparentProxy();
        }
      
        /// <summary>
        /// Invoke method
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns> 
        public override IMessage Invoke(IMessage msg)
        {
            var methodCallMessage = msg as IMethodCallMessage;
            var realType = typeof(T);
            var methodInfo = realType.GetMethod(methodCallMessage.MethodName);
            var aspects = methodInfo.GetCustomAttributes(typeof(IAspect), true);

            /*
             * Before invoke
             */
            foreach (var aspect in aspects)
                ((IAspect)aspect).Before(methodInfo,methodCallMessage);

            /*
             * Invoke
             */
            ReturnMessage returnMessage = Invoke(methodCallMessage);

            /*
             * After Invoke
             */
            foreach (var aspect in aspects)
                ((IAspect)aspect).After(methodCallMessage, returnMessage);

            return returnMessage;
        }

        private ReturnMessage Invoke(IMethodCallMessage methodCallMessage)
        {
            object result = methodCallMessage.MethodBase.Invoke(new T(), methodCallMessage.InArgs);
            ReturnMessage returnMessage = new ReturnMessage(result, null, 0, methodCallMessage.LogicalCallContext, methodCallMessage);
            return returnMessage;
        }
    }

    /// <summary>
    /// Aspect interface
    /// </summary>
    internal interface IAspect
    {
        void Before(MethodInfo methodInfo, IMethodCallMessage methodCallMessage);

        void After(IMethodCallMessage methodCallMessage, ReturnMessage returnMessage);
    }

    /// <summary>
    /// DateTimeDictionary holding performance data
    /// </summary>
    internal class DateTimeDictionary
    {
        private static Dictionary<string, DateTime> keyValuePairs;

        public static Dictionary<string, DateTime> KeyValuePairs
        {
            get
            {
                if (keyValuePairs==null)
                    keyValuePairs=new Dictionary<string, DateTime>();
                return keyValuePairs;
            }
        }
    }

    /// <summary>
    /// Logging attribute class 
    /// </summary>
    internal class LogAttribute : Attribute, IAspect
    {
        public LogAttribute()
        {
        }

        public void After(IMethodCallMessage methodCallMessage, ReturnMessage returnMessage)
        {

            StringBuilder logText = new StringBuilder();

            logText.Append("\nMethod Name: "+methodCallMessage.MethodName);

            logText.Append("\n\t InArgs");
            for (int i = 0; i < methodCallMessage.ArgCount; i++)
                logText.Append("\n\t"+methodCallMessage.GetArgName(i) +":" + methodCallMessage.GetArg(i).ToString());

            if (returnMessage.ReturnValue!=null)
            {
                logText.Append("\n\tResult:" + returnMessage.ReturnValue.ToString());
            }
            else
            {
                logText.Append("\n\tResult:void");
            }
            

            Console.WriteLine("Logging  after" + logText.ToString());
        }

        public void Before(MethodInfo methodInfo, IMethodCallMessage methodCallMessage)
        {
            Console.WriteLine("Logging before: " + methodInfo.Name);
        }
    }

    /// <summary>
    /// PerformanceCounter attribute
    /// </summary>
    internal class PerformanceCounterAttribute : Attribute, IAspect
    {
        private readonly Guid counterId;

        public PerformanceCounterAttribute()
        {
            counterId = Guid.NewGuid();
        }

        public void After(IMethodCallMessage methodInfo, ReturnMessage returnMessage)
        {
            DateTime start = DateTimeDictionary.KeyValuePairs[counterId.ToString()];

            double serviceTime = DateTime.Now.Subtract(start).TotalMilliseconds;

            Console.WriteLine("Performance counter After, Service Time: "+  serviceTime);

            DateTimeDictionary.KeyValuePairs.Remove(counterId.ToString());
        }

        public void Before(MethodInfo methodInfo,IMethodCallMessage methodCallMessage)
        {
            DateTimeDictionary.KeyValuePairs.Add(counterId.ToString(), DateTime.Now);
            Console.WriteLine("Performance counter before");
        }
    }
}