Default
====
새로 추가된 필드에 기본값을 설정합니다.<br>
이미 존재하던 문서들에게만 적용됩니다.

```c#
[OldModels]
class Model_1 {
  public class Player : Model {
    public String name {get;set;}
  }
}

[NewModels]
class Model_2 {
  public class Player : Model {
    public String name {get;set;}
    
    [Default(100)]
    public int money {get;set;}
  }
}
```
