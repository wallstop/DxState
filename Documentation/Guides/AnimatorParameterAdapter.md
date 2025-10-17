# Animator Parameter Adapter

This guide explains how to drive Animator parameters from DxState by attaching `AnimatorParameterState` instances to your stacks.

## Requirements
- Unity's Animator component with parameters defined in the controller.
- DxState  (the adapter surface lives under `Runtime/State/Stack/States/Animator`).

## Setup
1. Create an empty `GameObject` under your `StateStackManager`.
2. Add a `GameState` component (or use `SerializedReference` to hold states).
3. Add an `AnimatorParameterState` asset or component:
   - Assign the Animator reference.
   - Choose the parameter type (Trigger/Bool/Float/Int).
   - Enter the parameter name exactly as it appears in the controller.
   - Configure the value to push (float/int/bool) or toggle the trigger.
   - Enable **Reset On Exit** if you need the parameter cleared when the state leaves.
4. Add the state to your stack graph or register it programmatically.

## Usage
- When the state enters, the adapter sets the configured parameter; exiting optionally resets it.
- Combine multiple adapters with composite states to drive complex Animator layers.

## Tips
- Use `CopyTransitionHistory` on the stack to verify the state entered when debugging Animator flows.
- Pair with `StateStackSnapshot` to freeze and restore complex Animator-driven stacks in tests.
