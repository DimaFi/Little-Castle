# Assets/_Game

Project-owned Unity content belongs here.

Suggested long-term layout:

```text
_Game/
├─ Scripts/
│  ├─ Core/
│  ├─ World/
│  ├─ Buildings/
│  ├─ Population/
│  ├─ Economy/
│  ├─ Combat/
│  ├─ UI/
│  └─ Save/
├─ Art/
├─ Materials/
├─ Prefabs/
├─ Scenes/
└─ Settings/
```

Do not create every possible folder preemptively. Add folders when a real subsystem needs them.

The current first subsystem is `Scripts/World`.
