# SentryNode

**Документација за Unity stealth AI проект**

Проектот е first-person stealth прототип изработен во Unity. Главниот фокус е guard AI систем кој патролира, гледа, слуша, станува сомничав, го брка играчот, истражува последна позната позиција и пребарува област пред да се врати во патрола.

> **Место за слика:** Главен поглед од демо нивото.  
> Вметни screenshot од first-person перспектива каде што се гледа ентериерот и барем еден guard NPC.

## 1. Основни информации

| Поле | Вредност |
| --- | --- |
| Предмет | AI for Games / Вештачка интелигенција за игри |
| Engine | Unity 2022.3.62f1 |
| Тип на проект | First-person stealth AI prototype |
| Главни сцени | `Assets/level1.unity`, `Assets/level1_working.unity`, `Assets/level-improvements.unity` |
| Главни системи | Behavior tree, vision, hearing, patrol, search, alert, debug visualization |

## 2. Цел на проектот

Целта на проектот е да се прикаже комплетен, но разбирлив stealth AI систем. Наместо guard однесувањето да биде напишано како една голема hard-coded скрипта, логиката е поделена на повеќе независни компоненти:

- перцепција преку вид;
- перцепција преку звук;
- behavior tree за одлучување;
- NavMesh движење;
- патролирање;
- истрага и пребарување;
- debug визуелизација;
- аудио feedback за состојбите на guard-от.

Ова го прави проектот погоден за демонстрација на AI концепти во игри, особено за stealth механики.

## 3. Играчко искуство

Играчот се движи во first-person перспектива. Движењето е поврзано со stealth системот: sprint е побрз но создава повеќе шум, додека crouch е побавен и може да биде речиси тивок.

| Акција | Input |
| --- | --- |
| Движење | `W`, `A`, `S`, `D` |
| Гледање | Mouse |
| Sprint | `Left Shift` |
| Crouch | `C` |
| Toggle cursor lock | `Escape` |

> **Место за слика:** Движење, sprint или crouch.  
> Вметни screenshot каде играчот е блиску до cover или guard и може да се објасни stealth движењето.

## 4. Главен stealth loop

Главниот gameplay циклус е:

1. Играчот се движи низ нивото.
2. Guard NPC патролира користејќи NavMesh.
3. Ако играчот влезе во видното поле, `VisionSystem` ја зголемува suspicion вредноста.
4. Ако играчот создаде шум, `HearingSystem` го пренесува шумот до guard listener-ите.
5. Guard-от станува сомничав, истражува или започнува chase.
6. Ако го изгуби играчот, оди до последната позната позиција.
7. Guard-от пребарува неколку точки околу таа позиција.
8. Ако не го најде играчот, се враќа во патрола.

## 5. Архитектура на AI системот

Главната класа е `GuardAI`, но таа не ја содржи целата логика сама. Таа ги поврзува perception системите, navigation системот, behavior tree структурата и runtime context-от.

| Компонента | Улога |
| --- | --- |
| `Assets/Scripts/BehaviorTree` | Основни behavior tree јазли: `Selector`, `Sequence`, `ConditionNode`, `ActionNode` |
| `GuardAI.cs` | Централен coordinator за guard decision-making |
| `GuardRuntimeContext` | Shared runtime state за behavior nodes |
| `GuardAiAdapters` | Адаптери меѓу behavior nodes и Unity компоненти |
| `VisionSystem` | Field-of-view, line-of-sight, suspicion и last-known position |
| `HearingSystem` | Noise event систем |
| `PatrolSystem` | Random NavMesh patrol |
| `SearchSystem` | Пребарување околу last-known position |
| `LevelBuilder` | Editor алатка за генерирање демо ниво |

> **Место за слика:** Inspector со `GuardAI`.  
> Вметни screenshot од Unity Inspector каде се гледаат поврзаните системи и runtime debug state.

## 6. Behavior tree

Behavior tree системот овозможува guard-от да избира однесување според приоритет. Root node е `Selector`, а default branch-овите се подредени вака:

1. `Chase Sequence` - ако играчот е целосно детектиран, guard-от го брка.
2. `Suspicious Sequence` - ако guard-от има делумен визуелен сигнал, се врти кон сомнителната позиција.
3. `Investigate Sequence` - guard-от оди до last-known position и пребарува.
4. `Noise Sequence` - ако слушнал шум, оди кон noise source.
5. `Patrol` - fallback однесување кога нема закана.

Оваа структура е подобра од едноставен `if/else` систем затоа што branch-овите може да се додаваат или менуваат без целосно препишување на guard логиката.

> **Место за слика:** Behavior tree flow.  
> Вметни дијаграм или screenshot од debug overlay што покажува active node/state.

## 7. Guard состојби

| State | Опис |
| --- | --- |
| `Patrolling` | Guard-от избира random NavMesh точки и патролира |
| `Suspicious` | Има делумен сигнал и се врти кон сомнителна позиција |
| `Chasing` | Играчот е детектиран и guard-от го следи |
| `Investigating` | Guard-от оди до last-known position или noise source |
| `Searching` | Guard-от посетува повеќе search точки околу последната позната позиција |

## 8. VisionSystem

`VisionSystem` проверува:

- растојание до играчот;
- дали играчот е во field of view;
- дали има obstacle меѓу guard-от и играчот;
- proximity awareness за блиски ситуации;
- suspicion вредност од `0` до `100`.

Suspicion levels:

- `Unaware` - guard-от нема доволно информација;
- `Suspicious` - guard-от забележал нешто;
- `Detected` - играчот е доволно сигурно виден.

Кога suspicion е доволно висок и има line-of-sight, guard-от ја зачувува последната позната позиција на играчот.

> **Место за слика:** Vision cone и line-of-sight.  
> Вметни screenshot со `GuardVisionRenderer` или Gizmos што го покажуваат видното поле.

## 9. HearingSystem

`HearingSystem` работи како глобален broadcaster за шум. Кога играчот се движи, `PlayerController` повикува:

```csharp
HearingSystem.ReportNoise(transform.position, noiseRadius, HearingSystem.NoiseType.Footstep);
```

Секој guard што има активен `HearingSystem` проверува дали е во радиусот на шумот. Ако е, ја памети позицијата и behavior tree може да го испрати guard-от да ја истражи.

Движењето влијае вака:

- walking создава умерен шум;
- sprinting создава поголем шум;
- crouching може да биде без шум ако радиусот е поставен на 0.

> **Место за слика:** Guard истражува шум.  
> Вметни screenshot каде guard-от се движи кон позицијата од која играчот направил звук.

## 10. PatrolSystem

`PatrolSystem` не користи фиксна waypoint листа. Наместо тоа, избира random NavMesh точки околу patrol origin. Системот проверува дали точката е валидна, достижна и дали guard-от не е заглавен.

Ова дава поприродно wandering патролирање и овозможува повеќе guard-и да се однесуваат различно дури и со иста логика.

## 11. SearchSystem

`SearchSystem` се активира кога guard-от ќе стигне до последната позната позиција. Системот генерира неколку NavMesh точки околу таа позиција и guard-от ги посетува една по една.

Search завршува кога:

- сите точки се посетени; или
- search timer-от ќе ја достигне границата.

> **Место за слика:** Search points.  
> Вметни screenshot со `SearchSystem` Gizmos или guard во `Searching` состојба.

## 12. PlayerController

`PlayerController` е минимален first-person controller. Тој користи `CharacterController` за движење, mouse look за камера, crouch/sprint логика и footstep audio.

Најважно за AI системот е што `PlayerController` емитува noise events. Затоа player movement не е само input механика, туку директно влијае на guard decision-making.

## 13. GuardAlertSystem и GuardSoundSystem

`GuardAlertSystem` овозможува shared alert information, особено позицијата каде што играчот бил виден. Ова е основа за координација меѓу guard системи.

`GuardSoundSystem` додава аудио feedback за:

- влез во suspicious state;
- chase state;
- investigating state;
- враќање во patrol;
- patrol/chase/search movement loops.

> **Место за слика:** Guard state transition.  
> Вметни screenshot или забелешка од момент кога guard-от преминува од `Patrolling` во `Chasing`.

## 14. Demo Level Builder

`Assets/Editor/LevelBuilder.cs` содржи Unity Editor алатка за генерирање демо ниво. Се користи преку:

```text
Tools > Build Demo Level
```

Алатката:

- креира indoor layout со простории и коридори;
- додава cover објекти;
- креира player и guard варијанти;
- конфигурира layers и tags;
- ги поврзува AI компонентите;
- поставува audio clips;
- гради NavMesh.

> **Место за слика:** Tools > Build Demo Level.  
> Вметни screenshot од Unity menu или од сцената веднаш по генерирање на нивото.

## 15. Debug visualization

Проектот содржи неколку debug алатки за да се објасни што прави AI системот:

- `GuardVisionRenderer` го прикажува vision cone;
- `GuardDebugVisualizer` покажува state и active node информации;
- `VisionSystem.OnDrawGizmos` црта FOV и raycast линии;
- `HearingSystem.OnDrawGizmos` црта noise radius;
- `SearchSystem.OnDrawGizmos` ги прикажува search точките.

> **Место за слика:** Debug visualization.  
> Вметни screenshot со активни Gizmos, vision cone, state label или search points.

## 16. Како може да се прошири проектот

За додавање ново guard однесување:

1. Креирај нов `GuardConditionNode` или `GuardActionNode`.
2. Имплементирај `IGuardBehaviorBranchProvider`.
3. Во `CreateBranch` врати `Sequence`, `Selector` или custom `Node`.
4. Постави `Order` вредност за приоритет.
5. Додај го provider-от во `DefaultGuardBehaviorBranches` или преку `branchProviderBehaviours` во Inspector.

Ова ја прави AI архитектурата отворена за надградба без целосно менување на постоечката логика.

## 17. Заклучок

Во проектот е изработен функционален stealth AI прототип со:

- first-person player movement;
- guard патролирање;
- визуелна перцепција;
- аудитивна перцепција;
- behavior tree decision-making;
- chase, investigation и search state;
- shared alert logic;
- audio feedback;
- debug visualization;
- editor-generated demo level.

Најважната вредност на проектот е чистата поделба на системите. Guard AI однесувањето е модуларно, читливо и подготвено за понатамошно проширување.

## 18. Screenshot checklist

За финалната PDF верзија, препорачани screenshots се:

- општ поглед од демо нивото;
- player crouch/sprint ситуација;
- guard vision cone;
- suspicious state;
- chase state;
- hearing/noise investigation;
- search points околу last-known position;
- Unity menu `Tools > Build Demo Level`;
- `GuardAI` Inspector со runtime debug полиња.

