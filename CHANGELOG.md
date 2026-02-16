# Changelog - v0.5

## Input System
- SDL controller and mod hotkeys are now ignored when the game window is unfocused

## Audio Navigation
- Steam Audio HRTF integration
- Compass direction hotkey (U / RT+DPadLeft): announces which compass direction the camera is facing

## Navigation List
- Quest category: CSVB-parsed quest triggers with acceptance/completion filtering via scenario flags
- Toilets are fixed : You can now walk to any static toilet in the game
- Camera turns as you auto-walk, follows where the player is walking
- Closer approach distance (0.1 units), player faces NPCs/facilities on arrival
- Region name announced on zone change
- Defeated enemies and recruited NPCs removed from list immediately
- Ghost transitions from other maps filtered out (mostly)

## Partner Status
- Single hotkey per partner (F3/F4 or RT+DPadUp and RT+DPadDown) announces live stats: HP, MP, mood, discipline, tiredness, curse, weight, age

## Dialog and TTS
- Enemy detection message uses say queue instead of say. Means it doesn't interupt tutorial messages
- Button names now speak correctly in tutorials