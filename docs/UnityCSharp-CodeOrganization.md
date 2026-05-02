# Unity + C#: organizace projektu a dělení kódu

Stručný průvodce pro tento repozitář. Oficiální zdroje jsou autoritativní; níže je konkrétní návrh stromu, aby se omezil „spaghetti“ ve velkých `MonoBehaviour`.

## Doporučené externí zdroje (čti v tomto pořadí)

1. **[Organizing your Unity project](https://unity.com/how-to/organizing-your-project)** — pojmenování, složky, oddělení vlastních assetů od pluginů, sandbox.
2. **[Unity programming best practices](https://docs.unity3d.com/Manual/programming-best-practices.html)** — kompilace, domain reload, podmíněná kompilace.
3. **[Organizing scripts into assemblies](https://docs.unity3d.com/Manual/assembly-definition-files.html)** — `.asmdef` pro menší jednotky kompilace a jasné závislosti (vhodné až při větším počtu skriptů).
4. **[Predefined assemblies / compile order](https://docs.unity3d.com/Manual/script-compile-order-folders.html)** — složky `Editor`, `Plugins` a pořadí.
5. **[C# coding conventions (Microsoft)](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)** — styl pojmenování, layout, komentáře.

Photon Fusion: **[Fusion dokumentace / tutoriály](https://doc.photonengine.com/fusion/current/getting-started/fusion-intro)** drž vedle Unity pravidel (autorita, ticky).

## Principy pro menší „god“ skripty

- **Jedna hlavní odpovědnost** na třídu (už v `.cursor/rules/unity-architecture.mdc`).
- **NetworkBehaviour zůstává tenký**: čte vstup, volá služby, synchronizuje stav; složitá pravidla dej do plain C# tříd (bez `MonoBehaviour`), které lze unit-testovat mimo Play Mode, kde to dává smysl.
- **UI odděleně** od spawnu / sítě (`CombatHud` nesmí řídit gameplay).
- **Editor-only kód** jen za `#if UNITY_EDITOR` nebo v `Assets/.../Editor/` s vlastním `.asmdef`, ať se nedostane do buildu.

## Návrh stromu pro `Assets/Scripts` (postupná migrace)

Nemusíš hned přesouvat vše; při nových featurách drž tento tvar a staré soubory přesouvej po malých commitech.

```
Assets/Scripts/
  Core/              # sdílené typy: GameplayInput, enumy tlačítek, konstanty
  Networking/        # Fusion glue: spawner, callbacks na runneru (tenké)
  Player/            # hráč: movement, combat, health (NetworkBehaviour + případné služby)
  UI/                # HUD, OnGUI / UI Toolkit – jen čtení stavu / events
  Training/          # editor / dummy cíle (volitelné)
```

Příklad rozdělení současné logiky:

| Současná oblast | Směr | Poznámka |
|-----------------|-------|----------|
| `PlayerSpawner` | `Networking/` + tenké `Player/` spawn helper | Input latch může zůstat na spawneru nebo jít do `Core/InputLatch.cs` |
| `PlayerCombat`, `PlayerMovement`, `Health` | `Player/` | Čisté C# `TargetSelector` / `SpellCast` jako služby volané z `FixedUpdateNetwork` |
| `CombatHud` | `UI/` | |

## Kdy založit nový soubor

- Soubor přesáhne zhruba **~300 řádků** nebo obsahuje **nesouvisející** public API.
- Objeví se **druhý důvod ke změně** stejné třídy (např. spawn + input + UI v jednom).
- Stejná logika se **kopíruje** mezi scénami — extrahuj prefab + komponentu nebo službu.

## Cursor v tomto repu

- Pravidla: `.cursor/rules/` (`unity-architecture`, `fusion-networking`, `csharp-style`).
- Skill pro refaktoring struktury: `.cursor/skills/unity-code-organization/SKILL.md`.
