using System;
using System.Collections.Generic;

namespace RaahnSimulation
{
	public class ToggleText : Text
	{
		private List<string> toggleStrings;
		private int toggleIndex;

	    public ToggleText(Simulator sim, string defaultMsg) : base(sim, defaultMsg)
	    {
	        toggleIndex = 0;
            toggleStrings = new List<string>();
	        toggleStrings.Add(defaultMsg);
	    }

	    public override void Update()
	    {
	        base.Update();
	    }

        public override void UpdateEvent(Event e)
        {
            base.UpdateEvent(e);

            if (clicked)
                Toggle();
        }

	    public void AddString(string newString)
	    {
	        toggleStrings.Add(newString);
	    }

	    public void Toggle()
	    {
	        if (toggleIndex < toggleStrings.Count - 1)
	            toggleIndex++;
	        else
	            toggleIndex = 0;

	        SetText(toggleStrings[toggleIndex]);
	    }
	}
}
