# Text Adventure

screenshot

## Layout

Most screens in the game should consist of a title bar with a left and right panel below it.

The Title bar should include the game's title alone with the most basic of stats, such as HP and Gold.

There should be a blank space after the title bar.

## Main View

The left panel should show the room's title, the room's description, and the menu.

The right panel should show the drawn room.

The drawn room should be centered vertically within the right panel.

## Rooms

Rooms should be drawn using text characters like this 

```text
┌─────────────┐
│             │
=    Dining   │
│    Hall     │
└──────"──────┘
```

Width: 16 chars
Height: 5 chars

The room's title should be in the middle.

When the text is too large to fit on one line, it should be split into two lines.

When that's not enough, it can be split into three lines.

An = charater represents a door going East or West.

A " character represents a door going North or South.

## Manipulatives

A manipulative is an item in the game, such as a torch, an axe, or an apple.

Manipulatives with special uses have their IDs maintained in ```KnownManipulativeIds```.
