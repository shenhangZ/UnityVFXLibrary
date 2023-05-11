using UnityEngine;
using UnityEngine.VFX;
using System.Runtime.InteropServices;

namespace Fern.VFX
{
    [RequireComponent(typeof(VisualEffect))]
    public class FishFocking : MonoBehaviour
    {
        [Range(256, 8192)] public int nums = 1024;
        public Vector2 speedRange = Vector2.zero;
        public Vector3 forceWeight = Vector3.zero;
        public Vector3 perceptionRadius = Vector3.zero;

        public float maxForce = 0f;
        public GameObject targetObject = null;
        public float targetForce = 0f;
        public ComputeShader flockingCS = null;

        GraphicsBuffer positionBuffer;
        GraphicsBuffer smoothedPositionBuffer;
        GraphicsBuffer velocityBuffer;
        GraphicsBuffer smoothedVelocityBuffer;

        bool _needsReset = true;

        void ResetResources()
        {
            Release();

            InitializeBuffers();

            _needsReset = false;
        }

        void Start()
        {
            InitializeBuffers();
        }

        void FixedUpdate()
        {
            if (_needsReset) ResetResources();
            Simulation();
            RenderInstancedMesh();
        }

        void OnDestroy()
        {
            Release();
        }

        #region Private Functions

        private void InitializeBuffers()
        {
            VisualEffect vfx = GetComponent<VisualEffect>();
            vfx.Reinit();
            vfx.SetFloat("Nums", nums);

            positionBuffer =
                new GraphicsBuffer(GraphicsBuffer.Target.Structured, nums, Marshal.SizeOf(typeof(Vector3)));
            velocityBuffer =
                new GraphicsBuffer(GraphicsBuffer.Target.Structured, nums, Marshal.SizeOf(typeof(Vector3)));
            smoothedPositionBuffer =
                new GraphicsBuffer(GraphicsBuffer.Target.Structured, nums, Marshal.SizeOf(typeof(Vector3)));
            smoothedVelocityBuffer =
                new GraphicsBuffer(GraphicsBuffer.Target.Structured, nums, Marshal.SizeOf(typeof(Vector3)));

            Vector3[] positionArray = new Vector3[nums];
            Vector3[] velocityArray = new Vector3[nums];
            for (int i = 0; i < nums; i++)
            {
                positionArray[i] = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) *
                                   5f;
                float theta = Random.Range(-Mathf.PI, Mathf.PI);
                // 这行代码的目的是生成一个随机的球坐标系角度 phi。球坐标系中的 phi 表示从正 z 轴到其所在点的连线与正 z 轴之间的夹角。
                // 在这个代码中，phi 的随机范围是从 -1 到 1，即弧度制表示的 -90° 到 90°
                // `Mathf.Asin` 函数用于计算给定数值的反正弦值，即求角度。将之前步骤生成的随机数作为输入，
                // 计算结果为介于 -90° 到 90° 的一个随机角度，弧度制表示为 -π/2 到 π/2。
                float phi = Mathf.Asin(Random.Range(-1f, 1f));

                // Mathf.Cos(phi) * Mathf.Cos(theta)完整为 1 * Mathf.Cos(phi) * Mathf.Cos(theta)其中1为半径值
                // 这个表达式经过两个分解步骤，首先处理φ角度以在xy平面上得到投影向量，然后根据θ角度计算x分量。这样我们就可以将
                // 球坐标系中的向量转换到笛卡尔坐标系，从而得到速度向量的x分量。同样的方法也适用于计算y和z分量
                // `sin(φ) = z / r`  --> `z = r * sin(φ)` --> `z = sin(φ)`
                // `(speedRange.x + speedRange.y) * 0.5f`。这个表达式计算speedRange的范围的平均值，然后将其用于缩放速度向量。
                velocityArray[i] = 
                    new Vector3(Mathf.Cos(phi) * Mathf.Cos(theta), Mathf.Cos(phi) * Mathf.Sin(theta), Mathf.Sin(phi)) *
                    (speedRange.x + speedRange.y) * 0.5f;
            }

            positionBuffer.SetData(positionArray);
            velocityBuffer.SetData(velocityArray);
            smoothedPositionBuffer.SetData(positionArray);
            smoothedVelocityBuffer.SetData(velocityArray);
        }

        private void Simulation()
        {
            Vector3 targetPosition = Vector3.zero;
            if (targetObject)
                targetPosition = targetObject.transform.position;

            ComputeShader cs = flockingCS;
            int kernelID = cs.FindKernel("Flocking");

            uint threadSizeX, threadSizeY, threadSizeZ;
            cs.GetKernelThreadGroupSizes(kernelID, out threadSizeX, out threadSizeY, out threadSizeZ);
            int threadGroupSizeX = Mathf.CeilToInt(nums / (float)threadSizeX);

            cs.SetBuffer(kernelID, "_PositionBuffer", positionBuffer);
            cs.SetBuffer(kernelID, "_VelocityBuffer", velocityBuffer);
            cs.SetBuffer(kernelID, "_SmoothedPositionBuffer", smoothedPositionBuffer);
            cs.SetBuffer(kernelID, "_SmoothedVelocityBuffer", smoothedVelocityBuffer);
            cs.SetInt("_Nums", nums);
            cs.SetVector("_SpeedRange", speedRange);
            cs.SetVector("_ForceWeight", forceWeight);
            cs.SetVector("_PerceptionRadius", perceptionRadius);
            cs.SetFloat("_MaxForce", maxForce);
            cs.SetVector("_TargetPosition", targetPosition);
            cs.SetFloat("_TargetForce", targetForce);
            cs.SetFloat("_DeltaTime", Time.deltaTime);

            cs.Dispatch(kernelID, threadGroupSizeX, 1, 1);
        }

        void Release()
        {
            if (positionBuffer != null)
            {
                positionBuffer.Release();
                positionBuffer = null;
            }

            if (velocityBuffer != null)
            {
                velocityBuffer.Release();
                velocityBuffer = null;
            }

            if (smoothedPositionBuffer != null)
            {
                smoothedPositionBuffer.Release();
                smoothedPositionBuffer = null;
            }

            if (smoothedVelocityBuffer != null)
            {
                smoothedVelocityBuffer.Release();
                smoothedVelocityBuffer = null;
            }

            _needsReset = true;
        }

        void RenderInstancedMesh()
        {
            var vfx = GetComponent<VisualEffect>();
            if (smoothedPositionBuffer != null)
                vfx.SetGraphicsBuffer("PositionBuffer", smoothedPositionBuffer);
            if (smoothedVelocityBuffer != null)
                vfx.SetGraphicsBuffer("VelocityBuffer", smoothedVelocityBuffer);
        }

        #endregion
    }
}