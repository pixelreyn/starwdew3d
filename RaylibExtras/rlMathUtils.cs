using System;
using System.Numerics;
using System.Text;

namespace raylibExtras
{
    public static class rlMathUtils
    {
        public static float[] MatrixToBuffer(Matrix4x4 matrix)
        {
            float[] buffer = new float[16];

            buffer[0] = matrix.M11;
            buffer[1] = matrix.M21;
            buffer[2] = matrix.M31;
            buffer[3] = matrix.M41;
            
            buffer[4] = matrix.M12;
            buffer[5] = matrix.M22;
            buffer[6] = matrix.M32;
            buffer[7] = matrix.M42;
            
            buffer[8] = matrix.M13;
            buffer[9] = matrix.M23;
            buffer[10] = matrix.M33;
            buffer[11] = matrix.M43;
            
            buffer[12] = matrix.M14;
            buffer[13] = matrix.M24;
            buffer[14] = matrix.M34;
            buffer[15] = matrix.M44;

            return buffer;
        }
    }
}