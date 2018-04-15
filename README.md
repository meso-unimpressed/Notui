# ₪i
[![Build status](https://ci.appveyor.com/api/projects/status/bboskxevupreppj7?svg=true)](https://ci.appveyor.com/project/microdee/notui)

Notui is a renderless UI framework which is focusing on arbitrary elements and the user's interaction with them. Designed for but not exclusive to large scale media applications.

Notui operates with stateless prototype records (ElementPrototype) which carry only user updated information. These prototypes then used to instantiate state-ful elements per-context (NotuiElement). These element instances can be used for getting interaction states, transformations and reacting to user input events. This behavior allows to be Dataflow friendly and have a single source of truth. This means the developer can change the hierarchy structure of prototypes and let the context handle any special behavior regarding that change (like fading in/out for instance)

Its first implementation is done in vvvv where this implies that UI definition, interaction behavior and UI animation/rendering can be nicely separated into their own subpatches without resulting in useless cobwebs.

An element can be transformed in 3D space (position, scale and quaternion rotation) and it can have any hittesting function. In Notui interactions usually dubbed touches because the main targets are touchscreens but any pointing device works such as pen or plain mice.

An element has 2 types of transformation, one for display and one for interaction. When a touch starts to interact with an element first its Display Transformation is used while hittesting but during further interaction, the Interaction Transformation is used. This allows behaviors to have smoothing or other visual effects which would slide the element out from below the touch, but it should keep the interacting state.

Elements can have other elements as children so they form a hierarchy. Child elements are inheriting their parent's Display Transformation and if their parents are deleted they'd also get deleted.

The default interaction behavior is:
- An element can be Active or not. While not active it won't receive touches but it will execute its behaviors
- A user starts interacting with the element when a touch is created on that element.
- That touch will interact with the element until released.
- Elements closer to the viewer are blocking touches and hits from elements beyond it, unless Transparent is turned on. All transparent elements which has a touch above them receive hitting touches until the closest non transparent element finally blocks it from elements beyond it.
- Elements also report raw but blocked hitting touches. So if a touch slides onto the element it will be "Hit" but not "Touched".
- Also Elements where touches were slided off from them will be "Touched" but not "Hit".
- Elements have 2 assigned timers an Age started from its creation and a funnily named Dethklok which is started when its deletion is requested.
- This means elements can inherently fade in to life and fade out to deletion.
- This also means in vvvv newly created element slices can fade in and deleted or gone slices are kept in the Context while they're fading out.

Each element can also have separate list of optional behaviors which either override or augment the above ones, introducing user driven animations and interaction schemes for example.