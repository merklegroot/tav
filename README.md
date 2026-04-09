# Text Adventure

![screenshot](img/2026-04-09-01-07-59.png)

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

### Menu

The Menu should show the action key in parentheses.

The action key should always be in caps.

The parentheses should be a brighter color than the rest of the text, except for the action key which should be even brighter than the prentheses.

When the action key isn't part of the word, then the action key should be to the left of the menu item.

Examples:

(N)orth -- Acion key is at the beginning of the word.

e(X)it -- Action key was still inside of the word. Since it's the second character, the first character is no longer capilalized to empahsize that the first letter isn't the action key.

(1) Torch -- Action isn't in the word, so it's to the left of it.


