using UnityEngine;

namespace RT
{
	[RequireComponent(typeof(Camera))]
	public class RayTracing : MonoBehaviour
	{
		[SerializeField]
		private ComputeShader rtComputeShader = null;

		[SerializeField]
		private Texture skyboxTexture2D = null;
		
		private RenderTexture renderTexture = null;

		private Camera camera = null;
		
		private const float threadBlockSize = 8.0f;

		private void Awake()
		{
			camera = GetComponent<Camera>();
		}

		private void UpdateRenderTexture()
		{
			if (renderTexture != null)
			{
				renderTexture.Release();
			}

			RenderTextureFormat textureFormat = RenderTextureFormat.ARGBFloat;
			RenderTextureReadWrite textureReadWrite = RenderTextureReadWrite.Linear;
			
			renderTexture = new RenderTexture(Screen.width,Screen.height,0, textureFormat,textureReadWrite );
			renderTexture.enableRandomWrite = true;
			renderTexture.Create();
		}

		private void OnRenderImage(RenderTexture src, RenderTexture dest)
		{
			SetShaderParameters();
			Render(dest);
		}

		private void Render(RenderTexture dest)
		{
			UpdateRenderTexture();
			
			rtComputeShader.SetTexture(0, "result", renderTexture);
			int threadGroupSizeX = Mathf.CeilToInt(Screen.width / threadBlockSize);
			int threadGroupSizeY = Mathf.CeilToInt(Screen.width / threadBlockSize);
			rtComputeShader.Dispatch(0,threadGroupSizeX,threadGroupSizeY,1);
			
			Graphics.Blit(renderTexture, dest);
		}

		private void SetShaderParameters()
		{
			rtComputeShader.SetTexture(0,"skyboxTexture", skyboxTexture2D);
			
			rtComputeShader.SetMatrix("cameraToWorld", camera.cameraToWorldMatrix);
			rtComputeShader.SetMatrix("cameraInverseProjection", camera.projectionMatrix.inverse);
		}
		
		
	}

}
