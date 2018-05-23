# noye
an irc bot.

## how to write modules
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
