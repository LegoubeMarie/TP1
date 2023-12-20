using System.Collections.Generic;
using System.Linq;
using Tavern.ActionModule;
using Tavern.StateModule;
using UnityEngine;

namespace Tavern.AIModule
{
    public class ActionPlanner : MonoBehaviour, IActionPlanner
    {
        /// <summary>
        /// Plan selector policy
        /// </summary>
        IPlanSelector _planSelector;

        /// <summary>
        /// Actions to do by character
        /// </summary>
        private List<IAction> planned_actions;

        /// <summary>
        /// Apply action effect/required on states
        /// </summary>
        private IStateSetActionUpdater _actionStateUpdater;

        public virtual void Start()
        {
            _planSelector = GetComponent<IPlanSelector>();
            _actionStateUpdater = GetComponent<IStateSetActionUpdater>();
        }


        /// <summary>
        /// Find action sequence starting from agentState and reaching goalState using actions
        /// </summary>
        /// <param name="goalState">State to reach using actions</param>
        /// <param name="agentStates">Current character states</param>
        /// <param name="actions">Available actions</param>
        /// <param name="parent">Previous action are stored in parent node</param>
        /// <returns>List with leaf nodes of plans</returns>
        private List<PlanNode> FindActionSequence(in StateSet goalState, in StateSet agentStates, in List<IAction> actions, PlanNode parent)
        {
            List<PlanNode> leaves = new();
            foreach (IAction action in actions)
            {
                // Action fulfill at least one goal requirement ?
                if (!StateSetSatisfyingUtility.PartiallySatisfies(goalState, action.Effect()))
                {
                    continue;
                }

                // Copy goal states into current states
                StateSet currentStates = new(goalState);
                _actionStateUpdater.ApplyReverseActionOn(action, currentStates);

                bool isLeaf = StateSetSatisfyingUtility.Satisfies(currentStates, agentStates);
                // create node with current action
                PlanNode node = new(action, parent, isLeaf);
                if (isLeaf)
                {
                    leaves.Add(node);
                    continue;
                }

                List<IAction> remaining_actions = new(actions);
                remaining_actions.Remove(action);

                List<PlanNode> leaves_found = FindActionSequence(currentStates, agentStates, remaining_actions, node);
                foreach (PlanNode child in leaves_found)
                {
                    leaves.Add(child);
                }
            }
            return leaves;
        }

        /// <summary>
        /// Create an action plan to related character according to its goals and states
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IAction> CreatePlan(in List<Goal> goals, in StateSet states, in List<IAction> availableAction)
        {
            PlanNode rootNode = new();
            PlanNode bestPlan = null;
            if (_actionStateUpdater == null)
            {
                return new List<Action>();
            }
            foreach (Goal goal in goals)
            {
                // find action sequence from character state to goal state
                List<PlanNode> leaves = FindActionSequence(goal, states, availableAction, rootNode);
                bestPlan = _planSelector.SelectPlan(leaves);
            }
            return PlanUtility.PlanToList(bestPlan);
        }

        public void Clear()
        {
            planned_actions.Clear();
        }


        /// <summary>
        /// Return the next action to do according to plan
        /// </summary>
        /// <returns>The next action in plan</returns>
        public IAction GetNextAction(in List<Goal> goals, in StateSet states, in List<IAction> availableAction)
        {

            if ((planned_actions?.Count ?? 0) <= 0)
            {
                planned_actions = CreatePlan(goals, states, availableAction).ToList();
            }

            if (planned_actions.Count <= 0)
            {
                Debug.Log("No plan found");
                return null;
            }

            IAction nextAction = planned_actions[0];
            planned_actions.RemoveAt(0);
            return nextAction;
        }
    }
}
