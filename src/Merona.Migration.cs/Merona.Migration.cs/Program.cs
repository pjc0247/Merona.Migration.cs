using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;

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
        protected class Index : Attribute
        {
        }
    }

    class Program
    {
        // TODO 타입 바뀌는것도 체크해야 함

        /// <summary>
        /// 두 모델 간의 변경 사항을 찾는다.
        /// </summary>
        /// <param name="old">이전 버전 모델</param>
        /// <param name="to">마이그레이션할 대상 모델</param>
        static void FindDiffProps(Type old, Type to)
        {
            var oldProps = old.GetProperties();
            var toProps = to.GetProperties();

            var mutual = from toProp in toProps
                         join oldProp in oldProps on toProp.Name equals oldProp.Name
                         select toProp;

            var added = from toProp in toProps
                        where
                          !(from oldProp in oldProps
                            select oldProp.Name).Contains(toProp.Name)
                            ||
                           !(from oldProp in oldProps
                             where toProp.Name == oldProp.Name
                             select oldProp.PropertyType).First().IsEquivalentTo(toProp.PropertyType)
                        select toProp;
            var removed = from oldProp in oldProps
                          where
                            !(from toProp in toProps
                             select toProp.Name).Contains(oldProp.Name)
                             ||
                            !(from toProp in toProps
                              where toProp.Name == oldProp.Name
                              select toProp.PropertyType).First().IsEquivalentTo (oldProp.PropertyType)
                          select oldProp;

            foreach (var prop in mutual)
                Console.WriteLine(prop.Name);
            Console.WriteLine();

            foreach (var prop in added)
                Console.WriteLine(prop.Name);
            Console.WriteLine();

            foreach (var prop in removed)
                Console.WriteLine(prop.Name);
        }

        /// <summary>
        /// 두 모델 집합 간의 변경 사항을 찾는다.
        /// </summary>
        /// <param name="old">이전 버전의 모델 집합</param>
        /// <param name="to">마이그레이션할 대상 모델 집합</param>
        static void FindDiffModels(Type old, Type to)
        {
            var oldModels = old.GetNestedTypes();
            var toModels = to.GetNestedTypes();

            var mutual = from toModel in toModels
                         join oldModel in oldModels on toModel.Name equals oldModel.Name
                         select toModel;

            var added = from toModel in toModels
                        where
                          !(from oldModel in oldModels
                            select oldModel.Name).Contains(toModel.Name)
                        select toModel;
            var removed = from oldModel in oldModels
                          where
                            !(from toModel in toModels
                              select toModel.Name).Contains(oldModel.Name)
                          select oldModel;
        }

        static void Main(string[] args)
        {

        }
    }
}
