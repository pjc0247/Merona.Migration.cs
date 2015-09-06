using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Merona.Migration
{
    /// <summary>
    /// 이전 모델들을 담고 있는 클래스임을 나타내는 속성입니다.
    /// </summary>
    public class OldModels : Attribute
    {
    }
    /// <summary>
    /// 마이그레이션할 모델들을 담고 있는 클래스임을 나타내는 속성입니다.
    /// </summary>
    public class NewModels : Attribute
    {
    }

    public class Model
    {
        /// <summary>
        /// 해당 필드에 인덱스를 생성함을 나타내는 속성입니다.
        /// </summary>
        internal protected class Index : Attribute
        {
        }
    }
    
    public class Helper
    {
        private class Pair<T>
        {
            public T old { get; set; }
            public T to { get; set; }
        }
        private class ModelPair : Pair<Type> { }
        private class PropertyPair : Pair<PropertyInfo> { }

        private static IMongoDatabase database { get; set; }

        /// <summary>
        /// 두 모델 간의 변경 사항을 찾는다.
        /// </summary>
        /// <param name="old">이전 버전 모델</param>
        /// <param name="to">마이그레이션할 대상 모델</param>
        private static void FindDiffProps(
            Type old, Type to,
            out IEnumerable<PropertyPair> mutual, out IEnumerable<PropertyPair> added,
            out IEnumerable<PropertyPair> removed)
        {
            var oldProps = old.GetProperties();
            var toProps = to.GetProperties();

            mutual = from toProp in toProps
                         join oldProp in oldProps on toProp.Name equals oldProp.Name
                         select new PropertyPair { old = oldProp, to = toProp };

            added = from toProp in toProps
                        where
                          !(from oldProp in oldProps
                            select oldProp.Name).Contains(toProp.Name)
                            ||
                           !(from oldProp in oldProps
                             where toProp.Name == oldProp.Name
                             select oldProp.PropertyType).First().IsEquivalentTo(toProp.PropertyType)
                        select new PropertyPair { to = toProp };
            removed = from oldProp in oldProps
                          where
                            !(from toProp in toProps
                              select toProp.Name).Contains(oldProp.Name)
                             ||
                            !(from toProp in toProps
                              where toProp.Name == oldProp.Name
                              select toProp.PropertyType).First().IsEquivalentTo(oldProp.PropertyType)
                        select new PropertyPair { old = oldProp };
        }

        /// <summary>
        /// 두 모델 집합 간의 변경 사항을 찾는다.
        /// </summary>
        /// <param name="old">이전 버전의 모델 집합</param>
        /// <param name="to">마이그레이션할 대상 모델 집합</param>
        private static void FindDiffModels(
            Type old, Type to,
            out IEnumerable<ModelPair> mutual, out IEnumerable<ModelPair> added,
            out IEnumerable<ModelPair> removed)
        {
            var oldModels = old.GetNestedTypes();
            var toModels = to.GetNestedTypes();

            mutual = from toModel in toModels
                         join oldModel in oldModels on toModel.Name equals oldModel.Name
                         select new ModelPair{ old = oldModel, to = toModel };

            added = from toModel in toModels
                        where
                          !(from oldModel in oldModels
                            select oldModel.Name).Contains(toModel.Name)
                        select new ModelPair { to = toModel };
            removed = from oldModel in oldModels
                          where
                            !(from toModel in toModels
                              select toModel.Name).Contains(oldModel.Name)
                          select new ModelPair { old = oldModel };
        }
        
        public static void Config(String databaseName)
        {
            var mongo = new MongoClient();
            database = mongo.GetDatabase(databaseName);
        }
        public static void Config(MongoClientSettings setting, String databaseName)
        {
            var mongo = new MongoClient(setting);
            database = mongo.GetDatabase(databaseName);
        }
        public static void Config(String connectionString, String databaseName)
        {
            var mongo = new MongoClient(connectionString);
            database = mongo.GetDatabase(databaseName);
        }

        /// <summary>
        /// 변경된 모델에 대해 자동 마이그레이션을 수행한다.
        /// </summary>
        public static void AutoMigrate()
        {
            database = new MongoClient().GetDatabase("test");

            var types = Assembly.GetEntryAssembly().GetTypes();

            var oldModels = from type in types
                            where type.GetCustomAttribute<OldModels>() != null
                            select type;
            var toModels = from type in types
                           where type.GetCustomAttribute<NewModels>() != null
                           select type;

            if (oldModels.Count() != 1)
                throw new InvalidOperationException();
            if (toModels.Count() != 1)
                throw new InvalidOperationException();

            IEnumerable<ModelPair> mutual, added, removed;
            FindDiffModels(
                oldModels.First(), toModels.First(), 
                out mutual, out added, out removed);

            ProcessMutualModels(mutual);
            ProcessAddedModels(added);
            ProcessRemovedModels(removed);
        }

        private static async void ProcessMutualModels(IEnumerable<ModelPair> models)
        {
            Console.WriteLine("ProcesMutualModels");

            // 1. 삭제된 프로퍼티를 지운다.
            // 2. 추가된 프로퍼티를 생성한다.
            // 3. 타입이 바뀐 프로퍼티에 대해 타입을 바꾼다.
            // 4. 인덱스 변동사항을 처리한다.
            foreach (var pair in models)
            {
                Console.WriteLine("  {0}", pair.old.Name);

                IEnumerable<PropertyPair> mutual, added, removed;
                FindDiffProps(
                    pair.old, pair.to,
                    out mutual, out added, out removed);
            }
        }

        private static async void ProcessAddedModels(IEnumerable<ModelPair> models)
        {
            Console.WriteLine("ProcessAddedModels");

            // 1. Model을 순회하면서 인덱스가 필요한 프로퍼티에 대해
            //    인덱스를 생성한다.
            foreach (var pair in models)
            {
                Console.WriteLine("  {0}", pair.to.Name);

                var props = pair.to.GetProperties();

                var indexes = from prop in props
                              where prop.GetCustomAttribute<Model.Index>() != null
                              select prop;

                Console.WriteLine("    Create indexes....");
                foreach(var prop in props)
                {
                    Console.WriteLine("      field : {0}", prop.Name);
                    await database.GetCollection<BsonDocument>(pair.to.Name)
                        .Indexes.CreateOneAsync(
                            Builders<BsonDocument>.IndexKeys.Ascending(prop.Name));
                }
            }
        }

        private static async void ProcessRemovedModels(IEnumerable<ModelPair> models)
        {
            Console.WriteLine("ProcessRemovedModels");

            // 1. 존재하는 모델을 삭제한다.
            // 2. 인덱스를 삭제한다.
            foreach (var pair in models)
            {
                Console.WriteLine("  {0}", pair.old.Name);

                Console.WriteLine("    Drop all documents....");
                await database.GetCollection<BsonDocument>(pair.old.Name)
                    .DeleteManyAsync(new BsonDocument());

                Console.WriteLine("    Drop all indexes....");
                await database.GetCollection<BsonDocument>(pair.old.Name)
                    .Indexes.DropAllAsync();
            }
        }
    }


    [OldModels]
    public class Models_2015_0
    {
        public class Jinwoo : Model
        {
            [Index]
            public String iidex { get; set; }
        }
    }
    [NewModels]
    public class Models_2015_1
    {
        public class Player : Model
        {
            [Index]
            public String name { get; set; }

            public int level { get; set; }
            public int gold { get; set; }
        }
        public class Log : Model
        {
            [Index]
            public String id { get; set; }

            public String msg { get; set; }
        }
    }

    class Program
    {
        

        static void Main(string[] args)
        {
            Helper.AutoMigrate();

            System.Threading.Thread.Sleep(1000);
        }
    }
}
