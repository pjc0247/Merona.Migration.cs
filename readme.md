Merona.Migration.cs
====

Model 스키마 마이그레이션을 도와주는 툴입니다.<br>

```c#
[OldModels]
class Models_2015_09_03 {
  public class Player : Model {
    public String name {get;set;}
    public int level {get;set;}
    public int money {get;set;}
  }
}
```
```c#
[NewModels]
class Models_2015_09_04 {
  public class Player : Model {
    // name에는 인덱스가 추가됩니다.
    [Index]
    public String name {get;set;}
    // level 프로퍼티는 삭제됩니다.
    public int money {get;set;}
    // exp 프로퍼티는 추가됩니다.
    public int exp {get;set;}
  }

  // Log 모델은 추가됩니다.
  public class Log : Model {
    public String body {get;set;}
  }
}
```

```c#
/* DB 접속정보 설정 */
Merona.Migration.AutoMigrate();
```
