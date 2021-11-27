using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RT
{
	[RequireComponent(typeof(Camera))]
	public class RayTracing : MonoBehaviour
	{
		[CustomEditor(typeof(RayTracing))]
		private class RayTracingEditor : Editor
		{
			public override void OnInspectorGUI()
			{
				base.OnInspectorGUI();
				
				if (GUILayout.Button("Spawn Spheres"))
				{
					(target as RayTracing)?.CreateSpheres();
				}
			}
		}
		
		private struct Sphere
		{
			public Vector3 position;
			public float radius;
			public Vector3 albedo;
			public Vector3 specular;
		};
		
		[SerializeField]
		private ComputeShader rtComputeShader = null;

		[SerializeField]
		private Texture skyboxTexture2D = null;

		[SerializeField]
		private Light light = null;

		[SerializeField]
		private float minimalSphereRadius = 3.0f;

		[SerializeField]
		private float maximalSphereRadius = 8.0f;

		[SerializeField]
		private uint spheresCount = 100;

		[SerializeField]
		private float spherePlacementAreaRadius = 100.0f;

		[SerializeField]
		private float wavingSpeed = 10.0f;

		private ComputeBuffer sphereBuffer;
		
		private RenderTexture renderTexture = null;

		private Camera camera = null;
		
		private const float threadBlockSize = 8.0f;

		private readonly List<Sphere> spheres = new List<Sphere>();

		private void CreateSpheres()
		{
			spheres.Clear();
			
			for (int i = 0; i < spheresCount; i++)
			{
				float radius = minimalSphereRadius + Random.value * (maximalSphereRadius - minimalSphereRadius);
				Vector2 randomPos = Random.insideUnitCircle * spherePlacementAreaRadius;
				Vector3 position = new Vector3(randomPos.x, radius, randomPos.y);

				bool isIntersectingOthers = false;
				
				foreach (Sphere other in spheres)
				{
					float minDist = radius + other.radius;
					if (Vector3.SqrMagnitude(position - other.position) < minDist * minDist)
					{
						isIntersectingOthers = true;
						break;
					}
				}

				if (isIntersectingOthers)
				{
					continue;
				}
				
				Color color = Random.ColorHSV();
				bool metal = Random.value < 0.5f;
				Vector3 albedo = new Vector3(color.r, color.g, color.b);
				color = Random.ColorHSV();
				Vector3 specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.1f;

				spheres.Add(new Sphere()
				{
					position = position,
					radius = radius,
					albedo = albedo,
					specular = specular
				});
			}
			
			int stride = sizeof(float) * ( 3 * 3 + 1);
			
			sphereBuffer = new ComputeBuffer(spheres.Count, stride);
			sphereBuffer.SetData(spheres);
		}

		private void AnimateSpheres()
		{
			for (int i = 0; i < spheres.Count; i++)
			{
				Sphere sphere = spheres[i];
				sphere.position.y = sphere.radius * 1.5f + Mathf.Sin((Time.unscaledTime + i) * wavingSpeed) * sphere.radius;
				spheres[i] = sphere;
			}
			
			sphereBuffer.SetData(spheres);
		}
		
		private void Awake()
		{
			camera = GetComponent<Camera>();
			CreateSpheres();
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
			AnimateSpheres();

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

			Vector3 lightDirection = light.transform.forward;
			float lightIntensity = light.intensity;
			Vector4 lightVector = new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, lightIntensity);
			
			rtComputeShader.SetVector("directionalLight", lightVector);
			
			rtComputeShader.SetMatrix("cameraToWorld", camera.cameraToWorldMatrix);
			rtComputeShader.SetMatrix("cameraInverseProjection", camera.projectionMatrix.inverse);
			
			rtComputeShader.SetBuffer(0, "spheres", sphereBuffer);
		}
	}
}
