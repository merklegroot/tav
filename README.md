# Text Adventure

screenshot

## Layout

Most screens in the game should consist of a title bar with a left and right panel below it.

The Title bar should include the game's title alone with the most basic of stats, such as HP and Gold.

There should be a blank space after the title bar.

## Main View

The left panel should show the room's title, the room's description, and the menu.

The right panel should show the drawn room and below it the compass control.

The drawn room should be centered vertically within the right panel.

## Rooms

Rooms should be drawn using text characters like this 

```text
┌─────────────┐
│             │
=    Dining   │
│     Hall    │
└─────| |─────┘
```

Width: 16 chars
Height: 5 chars

The room's title should be in the middle.

When the text is too large to fit on one line, it should be split into two lines.

When that's not enough, it can be split into three lines.

An = charater represents a door going East or West.

Two pipes with a space between them represent a door going North or South.
| |

## Compass

The compass control should make it clear which directions are available to the user.

    N
    |
W -   - E
    |
    S

## Manipulatives

A manipulative is an item in the game, such as a torch, an axe, or an apple.

Manipulatives with special uses have their IDs maintained in ```KnownManipulativeIds```.

## Inventory

When an edible item from the inventory is selected
- The game should tell the user what effects eating it will have.
- The game should allow the user to eat it.
- The game should apply those effects.
