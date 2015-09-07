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
            public String name { get; set; }
            public String[] with { get; set; }
            public bool ascending { get; set; }
            public bool unique { get; set; }
        }

        /// <summary>
        /// 새로 추가된 프로퍼티일 때 기본값을 지정한다.
        /// 이 필드는 앞으로 추가될 Document에 대해서는 영향을 미치지 않는다.
        /// </summary>
        internal protected class Default : Attribute
        {
            internal object value { get; set; }

            public Default(object value)
            {
                this.value = value;
            }
        }
    }

    [OldModels]
    public class Models_2015_0
    {
        public class Player : Model
        {
            [Index]
            public String name { get; set; }

            public int level { get; set; }
            public int gold { get; set; }

            public int jinwoo { get; set; }
        }

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
            public String name { get; set; }

            [Index]            
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
            Helper.Config("test");
            Helper.AutoMigrate();

            System.Threading.Thread.Sleep(1000);
        }
    }
}
