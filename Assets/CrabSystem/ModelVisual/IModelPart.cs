// A component on a model prefab that needs its character's modules. ModelModule binds every one on
// each new model, after the equipment is back on. A model with no brain (a menu preview) is never
// bound, so a part must work unbound too.
public interface IModelPart
{
    void Bind(ControllerBrain brain, ModelModule model);
}
