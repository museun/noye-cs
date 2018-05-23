# noye
an irc bot.

## how to write modules
```csharp
// must inherit from the Noye.Module abstract class
public class MyModule: Module {
  private readonly string friend;

  // this ctor is required
  public MyModule(INoye noye): base(noye) {
    // you can do any setup here

    // such as loading a configuration
    var config = ModuleConfig.Get<MyModuleConfig>();
    friend = config.friend;

    // if it cannot successfully be loaded then
    // throw new CreationException();
  }

  // this is required, this is where you register your "handlers"
  public override void Register() {
    // any number of Command, Passive and Events can be registered here.

    // this is a command, which users trigger
    Noye.Command("hello", async env => {
      // when "!hello world" is seen
      if (env.Param == "world") {
        await Noye.Say(env, "indeed"); // => sends a message to #test with "indeed"
      }

      // when just "!hello" is seen
      if (env.Param == null) {
        await Noye.Reply(env, "hi!"); // => sends a message to #test with "testuser: hi!"
      }
    });

    // this is a passive, it uses a regex to match
    Noye.Passive(@"(?P<num>\d+)", Sum);

    // this is an event, it uses a raw event to get a raw message
    Noye.Event("KICK", async msg => {
      // this kicks anyone who kicks `this.friend` from the current channel. 

      // 0 is the channel, 1 is the person being kicked.
      if (msg.Parameters[1] == friend) {
        await Noye.Raw($"KICK {msg.Parameters[0]} {msg.Prefix.Split('!').First()} :don't do that");
        // this provides a "KICK #test_channel some_person :don't do that" message
      }
    });
  }


  // a method used in the passive handler to sum all numbers found with the regex
  private async Task Sum(Envelope env) {
    // this isn't really needed here
    if (!env.Matches.Has("num")) {
      return;
    }

    var sum = 0;
    // get all of the 'num' matches (a List<string>.)
    foreach (var num in env.Matches.Get("num")) {
      if (int.TryParse(num, out var n)) {
        sum += n;
      }
    }

    if(env.Matches.Get("num").Count > 0) {
      await Noye.Reply(env, $"I like to sum numbers: {sum}");
    }
  }

  // this is optional
  protected override void Dispose(bool disposing) {
    base.Dispose(disposing);
    // you can dispose if anything you need here
  }
}
```

- modules are classes that inherit from the `Noye.Module` abstract class
- `void Register()` must be overridden. this is where modules 'wire' up their interactions.
- the `Module(INoye noye)` constructor will store it in the base class. this can be used for interacting with the bot
- the modules have their own `HttpClient` in the baseclass, so they don't need to worry about gotchas that surround `using (var http = new HttpClient()) {}` leading to weird performance/resource penalities
- a `Dispose` method is also available if a module needs to be cleaned up before the bot shutdowns.
- an exception is available if the module cannot be created: `Module.CreationException`

**the bot will automatically find any classes that derive Module and run its Register() before starting, and its Dispose() when it exits**

## Configuration
refer to [Configuration.cs](Noye/Configuration.cs)

helpful methods for modules:
```csharp
// using
interface Noye.IModuleConfig {}
// ->
class MyConfig : IModuleConfig {}
// then you can use:
var conf = ModuleConfig.Get<MyConfig>();

// if you just want a simple configuration that stores an apikey then inherit from
// using
class Noye.ApiKeyConfig {}
// ->
class MyApiConfig : ApiKeyConfig {}
// then you can use:
var conf = ApiKeyConfig.Get<MyApiConfig>();
conf.Apikey; // is a string
```

## Envelope, Message and Matches.. and you.
### Envelopes
...are handed off to **'Command'** and **'Passive'** handlers.
#### it consists of 
| type | description |
| --- | --- |
|`string Sender` | who sent the message |
|`string Target` | where the message was sent |
|`string Param` | any parameters found after the !command. | 
|`Matches Matches` | a type that maps regex named groups to their values |

### Messages
...are handed off to the **'Event'** handlers.
#### it consists of 
| type | description |
| --- | --- |
|`string Prefix` | prefix (nick!user@hostname) of the event|
|`string Command` | event command as a string (PRIVMSG, 001, QUIT, etc)|
|`List<string> Parameters` | a list of parameters after the Command datum|
|`string Data` | the payload for the event (optional)|

example:

`:test!~testuser@irc.localhost PRIVMSG #test :this is an example of a PRIVMSG`

parses into

```csharp
Message {
 Prefix: "test!~testuser@irc.localhost"
 Command: "PRIVMSG"
 Parameters: ["#test"]
 Data: "this is an example of a PRIVMSG"
}
```

### Matches
...provided to **'Passive'** handlers.
```csharp
Noye.Passive(@"(?P<foo>\d{2}", async env => {
  env.Matches.Get("foo"); // a List<string> for the matches for any 'foo' in the PRIVMSG
  env.Matches.Has("bar"); // false  
});
```

----

## Handler registration:
### Commands
```csharp 
Command(string trigger, Func<Envelope, Task> func)
```
Commands are directly triggered by the users. such as `!hello world`

to add a command, in your module's `void Register()` function, use `Noye.Command("hello", helloHandler);`

you can use a lambda, or a method. it must have a signature of `async Task _(Envelope)`

if a lambda is desired then `async env => {}` will work.


### Passives
```csharp
Passive(string pattern, Func<Envelope, Task> func)
```
Passives are matches against their provided regex pattern.

to add a passive, in your module's `void Register()` function, use `Noye.Passive(@"(?P<name>)", reHandler);`

named groups are required, multiple groups can be matched

you can use a lambda, or a method. it must have a signature of `async Task _(Envelope)`

if a lambda is desired then `async env => {}` will work.


## Events
```csharp
void Event(string command, Func<Message, Task> func)
```
Events match their command against a raw event.

to add a passive, in your module's `void Register()` function, use `Noye.Event("EVENT_NAME", evHandler);`

you can use a lambda, or a method. it must have a signature of `async Task _(Message)`

if a lambda is desired then `async msg => {}` will work.

----

## interactions
refer to [INoye.cs](Noye/INoye.cs)

| method | args | encoded | purpose |
| --- | --- | --- | --- |
| `Say` | env, msg  | PRIVMSG $env.target :$msg | send a message to the target |
| `Reply` | env, msg | PRIVMSG $env.target :$env.sender: $msg | sends an addressed message to the target |
| `Emote` | env, msg | PRIVMSG $env.target :\1$msg\1 | sends an action to the target |
| `Raw` | data | $data | sends a raw message to the irc server |

```csharp
Task<bool> CheckAuth(Envelope)
```
allows you to determine if the sender of the envelope is an owner

```csharp
string GetHostAddress();
```
gets the host:port that the internal webserver is listening on.

```csharp
 T Resolve<T>();
```
resolves a dependency from the IoC container.


## helpful utilities
[Humanize.cs](Noye/Humanize.cs)

[NoyeExtensions.cs](Noye/NoyeExtensions.cs)
