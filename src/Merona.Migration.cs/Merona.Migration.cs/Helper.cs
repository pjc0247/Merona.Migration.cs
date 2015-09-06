using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

using MongoDB.Driver;
using MongoDB.Bson;

namespace Merona.Migration
{
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
                     select new ModelPair { old = oldModel, to = toModel };

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
        public static async void AutoMigrate()
        {
            if (database == null)
                throw new InvalidOperationException();

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

            foreach (var m in removed)
                Console.WriteLine(m.old.Name);

            await ProcessMutualModels(mutual);
            await ProcessAddedModels(added);
            await ProcessRemovedModels(removed);
        }

        private static async Task ProcessMutualModels(IEnumerable<ModelPair> models)
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

                Console.WriteLine("    Remove fields");
                foreach(var prop in removed)
                {
                    Console.WriteLine("      {0}", prop.old.Name);
                    database.GetCollection<BsonDocument>(pair.old.Name)
                        .UpdateManyAsync(
                            new BsonDocument(),
                            Builders<BsonDocument>.Update.Unset(prop.old.Name));
                }

                Console.WriteLine("    Modify indexes");
                foreach(var prop in mutual)
                {
                    // 인덱스 삭제됨
                    if (prop.old.GetCustomAttribute<Model.Index>() != null &&
                        prop.to.GetCustomAttribute<Model.Index>() == null)
                    {
                        Console.WriteLine("      remove : {0}", prop.old.Name);

                        // TODO : Descending
                        await database.GetCollection<BsonDocument>(pair.old.Name)
                            .Indexes.DropOneAsync(prop.old.Name + "_1");
                    }

                    // 인덱스 추가됨
                    if (prop.old.GetCustomAttribute<Model.Index>() == null &&
                        prop.to.GetCustomAttribute<Model.Index>() != null)
                    {
                        Console.WriteLine("      create : {0}", prop.old.Name);

                        await database.GetCollection<BsonDocument>(pair.old.Name)
                            .Indexes.CreateOneAsync(
                                Builders<BsonDocument>.IndexKeys.Ascending(prop.old.Name));
                    }
                }
            }
        }

        private static async Task ProcessAddedModels(IEnumerable<ModelPair> models)
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
                foreach (var prop in indexes)
                {
                    Console.WriteLine("      field : {0}", prop.Name);
                    await database.GetCollection<BsonDocument>(pair.to.Name)
                        .Indexes.CreateOneAsync(
                            Builders<BsonDocument>.IndexKeys.Ascending(prop.Name));
                }
            }
        }

        private static async Task ProcessRemovedModels(IEnumerable<ModelPair> models)
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
}
