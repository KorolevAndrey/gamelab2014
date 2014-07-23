﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GestureLib
{
    public class ShakingLeftRightGestureAlgorithm : IAccelerationGestureAlgorithm
    {
        #region IAccelerationGestureAlgorithm Members

        public float CalculateMatching(GestureStateCollection<AccelerationGestureState> gestureStates)
        {
            //Calculate the absolute value of the difference between minimum and maximum of the x-values
            float minX = gestureStates.Min(ags => ags.X);
            float maxX = gestureStates.Max(ags => ags.X);
            float diffX = Math.Abs(maxX - minX);

            //Calculate the absolute value of the difference between minimum and maximum of the y-values
            float minY = gestureStates.Min(ags => ags.Y);
            float maxY = gestureStates.Max(ags => ags.Y);
            float diffY = Math.Abs(maxY - minY);

            //Calculate the average of the y-values
            float avgY = Math.Abs(gestureStates.Average(ags => ags.Y));

            //Calculate the absolute value of the difference between minimum and maximum of the z-values
            float minZ = gestureStates.Min(ags => ags.Z);
            float maxZ = gestureStates.Max(ags => ags.Z);
            float diffZ = Math.Abs(maxZ - minZ);

            if (avgY > 0.2)
            {
                if (diffY + diffZ < diffX)
                {
                    return 0.91F;
                }
                else
                {
                    return 0.0F;
                }
            }
            else
            {
                return 0.0F;
            }
        }

        #endregion

        #region INamed Members

        public string Name { get; set; }

        #endregion
    }
}
