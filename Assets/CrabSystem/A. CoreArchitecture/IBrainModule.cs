// A module signs up with its brain by being a child of it; there are no reference lists to fill in.
// The brain initializes its modules in InitOrder, lowest first, and stays in that order for
// LateInitialize and the update loop. The core modules take 0-180 in steps of 10 (Identity 0,
// Faction 10, Model 20, Input 30, Camera 40, StateMachine 50, Movement 60, Animation 70, Ability 80,
// Stat 90, Resource 100, Blackboard 110, Damage 120, Inventory 130, RPG 140, Interaction 150,
// Dialogue 160, Hotbar 170, SlotTransformation 180). Anything that doesn't depend on start order
// keeps the default and runs after them.
public interface IBrainModule
{
    bool IsEnabled { get; set; }

    int InitOrder => 1000;

    // Declined on every entity that isn't the player: switched off and never initialized.
    bool PlayerOnly => false;

    void Initialize(ControllerBrain brain);

    void UpdateModule();

    // After every module has run Initialize, for wiring that needs the others ready.
    void LateInitialize() { }
}
