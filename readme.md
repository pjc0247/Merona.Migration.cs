Merona.Migration.cs
====

Model ��Ű�� ���̱׷��̼��� �����ִ� ���Դϴ�.<br>

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
    public String name {get;set;}
    // level ������Ƽ�� �����˴ϴ�.
    public int money {get;set;}
    // exp ������Ƽ�� �߰��˴ϴ�.
    public int exp {get;set;}
  }

  // Log ���� �߰��˴ϴ�.
  public class Log : Model {
    public String body {get;set;}
  }
}
```

```c#
/* DB �������� ���� */
Merona.Migration.AutoMigrate();
```