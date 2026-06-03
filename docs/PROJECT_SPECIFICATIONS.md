# PROJECT OVERVIEW

**Project name:** Monster Tamer: Guardians of Sarawak
**Language:** C# (.NET 8)
**Framework:** SplashKit SDK (via `SplashKit` NuGet package)
**Assignment:** COS20007 OOP Assignment — Option 4 (Custom Program with Design Patterns)
**Student:** Mohamad Haris Bin Yayusuf — Student ID: 105812190

## Concept

A turn-based creature-catching RPG themed entirely around Sarawak, Borneo wildlife and mythology. The player explores top-down tile-based regions, encounters wild monsters based on real Sarawak animals (Rhinoceros Hornbill, Proboscis Monkey, Pangolin, etc.), catches them using Capture Orbs, builds a party of up to 6 tamed monsters, battles AI trainers, and faces a legendary final boss — Naga Belian, a sea dragon from Borneo mythology.

## Current milestone

Milestone submission completed and approved. Feedback received from lecturer. **Final submission due 5th June — 3 days away as of last update (2026-06-02).** Priorities 1–5.2 are fully implemented. Remaining work: Priority 5.3–5.11 (UI/gameplay polish), Priority 6 (report updates — class diagram, sample output, roles tables).

## Assignment option requirements

- Option 3 baseline: 4 abstract base classes, full OOP principles demonstrated, SplashKit visual interface, comparable complexity to Battleships
- Option 4 additions: minimum 3 design patterns (Singleton, Factory, Strategy), updated class diagram showing patterns, justification of added complexity

## Lecturer feedback to address in final submission

1. Class diagram missing polymorphism — abstract methods must appear in abstract class boxes (in italics), and overriding methods must appear in child class boxes
2. Class diagram relationship arrows not reflected in class boxes — fields justifying composition/aggregation arrows must be shown inside the relevant boxes
3. Additional Features section was too generic — must describe mechanical complexity beyond the baseline, not just list features
4. Sample output needs to be more interactive — multiple screenshots or a screen recording showing actual gameplay flow


## Assignment constraints

- Language: C# only, .NET 8
- No external game engines (Unity, Godot, etc.)
- All code must be the student's own — do not copy-paste from online sources verbatim
- All classes must be in the `MonsterTamer` namespace
- Abstract methods must be marked `abstract` in the parent and `override` in every child
- Design patterns must be identifiable in the class diagram with UML note boxes


## Region layout

| Region              | Wild encounters           | Trainer        | Notes      |
| ------------------- | ------------------------- | -------------- | ---------- |
| Rainforest          | Bekantan, Pelanduk, Rajah | Ranger Siti    | Region 1   |
| River Delta         | Buaya, Mawas              | Ranger Azman   | Region 2   |
| Ancient Highlands   | Temaju, Enggang           | Champion Dayak | Region 3   |
| (after all regions) | —                         | Naga Belian    | Final boss |


## Sarawak monster roster

| Name        | Based on                | Type(s)         | Category               | Signature move |
| ----------- | ----------------------- | --------------- | ---------------------- | -------------- |
| Kelabit     | Rhinoceros Hornbill     | Flying/Grass    | TamedMonster (starter) | Casque Crash   |
| Bekantan    | Proboscis Monkey        | Normal/Water    | WildMonster            | Riverleap      |
| Mawas       | Bornean Orangutan       | Normal/Fighting | WildMonster            | Vine Slam      |
| Rajah       | Rajah Brooke's Birdwing | Electric/Flying | WildMonster            | Wing Spark     |
| Enggang     | Sun Bear                | Fighting/Grass  | WildMonster            | Honey Rage     |
| Pelanduk    | Lesser Mouse Deer       | Normal          | WildMonster            | Phantom Step   |
| Temaju      | Sunda Pangolin          | Steel/Normal    | WildMonster            | Scale Shield   |
| Buaya       | Saltwater Crocodile     | Water/Normal    | WildMonster            | Death Roll     |
| Naga Belian | Borneo sea dragon       | Water/Normal    | BossMonster (3 phases) | Tidal Surge    |


## Type advantage chart (8×8)

Rows = attacking type, Columns = defending type. Values: 2.0 = super effective, 1.0 = normal, 0.5 = not very effective, 0.0 = immune.

```
          Nrm   Fire  Watr  Grss  Elec  Fly   Fgt   Stl
Normal  [ 1.0,  1.0,  1.0,  1.0,  1.0,  1.0,  1.0,  0.5 ]
Fire    [ 1.0,  0.5,  0.5,  2.0,  1.0,  1.0,  1.0,  2.0 ]
Water   [ 1.0,  2.0,  0.5,  0.5,  1.0,  1.0,  1.0,  1.0 ]
Grass   [ 1.0,  0.5,  2.0,  0.5,  1.0,  0.5,  1.0,  0.5 ]
Electric[ 1.0,  1.0,  2.0,  0.5,  0.5,  2.0,  1.0,  1.0 ]
Flying  [ 1.0,  1.0,  1.0,  2.0,  0.5,  1.0,  2.0,  0.5 ]
Fighting[ 2.0,  1.0,  1.0,  1.0,  1.0,  0.5,  1.0,  2.0 ]
Steel   [ 1.0,  0.5,  1.0,  2.0,  1.0,  1.0,  1.0,  0.5 ]

```

## Key decisions (do not change without discussion)
- Monster names in Malay/Iban — non-negotiable, part of the unique theme
- Geometric sprites only — no external PNG assets unless a suitable Sarawak-themed pack is found and purchased
- 3 regions + final boss — scope is fixed
- Player starter: Kelabit (Flying/Grass) at level 8
- TypeChart: 8 types only
- Option 4 confirmed — Singleton + Factory + Strategy
- Window target size: 1280×720
- **Due date: 5th June**