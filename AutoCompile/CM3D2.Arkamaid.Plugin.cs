using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityInjector;
using UnityInjector.Attributes;

namespace CM3D2.Arkamaid
{
	public class BlockMaterial
	{
		public int Life = 5;
		public Color Color = Color.white;
	}

	[PluginFilter("CM3D2x64"), PluginFilter("CM3D2x86"), PluginName("Arkamaid"), PluginVersion("0.0.0.0")]
	public class Arkamaid : PluginBase
	{
		public class Config
		{
			public KeyCode KeyBoot = KeyCode.Space;
			public KeyCode KeyStart = KeyCode.Space;
			public KeyCode KeyAutoPlay = KeyCode.Return;
			public float BallSpeed = 0.1f;
			public string Stage = string.Empty;
		}

		public class Stage
		{
			public Vector3 GameScreenOffset = Vector3.zero;
			public float Scale = 0.01f;
			public int BlockNumX = 20;
			public int BlockNumY = 20;
			public List<BlockMaterial> BlockMaterials = new List<BlockMaterial>();
			public string BlockTable = string.Empty;
		}

		public enum STATE
		{
			UNINITIALIZED,
			INITIALIZED,
		}

		public Config config = new Config();
		public Stage stage = new Stage();
		private STATE m_state = STATE.UNINITIALIZED;

		public void Awake()
		{
			UnityEngine.Object.DontDestroyOnLoad(this);
			config = loadXml<Config>(System.IO.Path.Combine(this.DataPath, "Arkamaid/Arkamaid.xml"));
		}

		public void Update()
		{
			switch (m_state)
			{
				case STATE.UNINITIALIZED:
					if (Input.GetKeyDown(config.KeyBoot))
					{
						setupStage();
						m_state = STATE.INITIALIZED;
					}
					break;

				case STATE.INITIALIZED:
					{
						var anyBlock = GameObject.Find("Block");
						if (anyBlock == null)
						{
							var arkamaid = GameObject.Find("Arkamaid");
							if (arkamaid != null) UnityEngine.Object.Destroy(arkamaid);
							m_state = STATE.UNINITIALIZED;
						}
						else
						{
							var arkamaid = GameObject.Find("Arkamaid");
							if (Input.GetKeyDown(config.KeyBoot))
							{
								UnityEngine.Object.Destroy(arkamaid);
								m_state = STATE.UNINITIALIZED;
							}
							else
							{
								arkamaid.transform.parent = GameMain.Instance.MainCamera.transform;
								arkamaid.transform.localPosition = stage.GameScreenOffset;
								arkamaid.transform.localRotation = Quaternion.identity;
							}
						}
					}
					break;
			}
		}

		private void setupStage()
		{
			config = loadXml<Config>(System.IO.Path.Combine(this.DataPath, "Arkamaid/Arkamaid.xml"));
			stage = loadXml<Stage>(System.IO.Path.Combine(this.DataPath, "Arkamaid/stages/" + config.Stage));

			var arkamaid = GameObject.Find("Arkamaid");
			if (arkamaid != null) UnityEngine.Object.Destroy(arkamaid);
			arkamaid = new GameObject("Arkamaid");
			arkamaid.AddComponent<ArkamaidMain>();

			ArkamaidMain.RootDir = System.IO.Path.Combine(this.DataPath, "Arkamaid");
			ArkamaidMain.KeyAutoPlay = config.KeyAutoPlay;
			ArkamaidMain.KeyStart = config.KeyStart;
			ArkamaidMain.BallSpeed = config.BallSpeed * stage.Scale;
			ArkamaidMain.BlockSize = stage.Scale;
			ArkamaidMain.BlockThickness = stage.Scale * 0.125f;
			ArkamaidMain.BlockNumX = stage.BlockNumX;
			ArkamaidMain.BlockNumY = stage.BlockNumY;
			ArkamaidMain.BlockTable = new int[stage.BlockNumX * stage.BlockNumY];
			ArkamaidMain.BlockMaterials = stage.BlockMaterials;

			int i = 0;
			foreach (var s in stage.BlockTable.Split(new char[] { ',' }))
			{
				ArkamaidMain.BlockTable[i] = int.Parse(s);
				if (++i >= ArkamaidMain.BlockTable.Length) break;
			}
		}

		public void LateUpdate()
		{
			var r = UnityEngine.Random.Range(-Mathf.PI, Mathf.PI);
			var v = new Vector3(Mathf.Sin(r), Mathf.Cos(r), 0.0f) * ArkamaidMain.ShakeOffsetScale;
			var arkamaid = GameObject.Find("Arkamaid");
			if (arkamaid == null) return;
			ArkamaidMain.ShakeOffsetScale = ArkamaidMain.ShakeOffsetScale * 0.75f;
		}

		private T loadXml<T>(string path)
		{
			var serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
			using (var sr = new System.IO.StreamReader(path, new System.Text.UTF8Encoding(true)))
			{
				return (T)serializer.Deserialize(sr);
			}
		}
	}

	public class ArkamaidMain : MonoBehaviour
	{
		public static string RootDir = string.Empty;
		public static float BallSpeed = 4.0f;
		public static float BlockSize = 0.1f;
		public static float BlockThickness = 0.01f;
		public static int BlockNumX = 20;
		public static int BlockNumY = 20;
		public static List<BlockMaterial> BlockMaterials = null;
		public static int[] BlockTable = null;
		public static KeyCode KeyAutoPlay = KeyCode.Space;
		public static KeyCode KeyStart = KeyCode.Return;
		public static float ShakeOffsetScale = 0.0f;
		public static bool AutoPlay = false;

		private PhysicMaterial phymat = null;
		private Material material = null;
		private Material[] blockMaterials = null;
		internal static AudioSource[] se = null;

		void Start()
		{
			Physics.bounceThreshold = 0.0f;

			float l = -BlockNumX * 0.5f * BlockSize;
			float r = +BlockNumX * 0.5f * BlockSize;
			float u = +BlockNumY * 0.5f * BlockSize + BlockSize * 0.5f;
			float d = -BlockNumY * 0.5f * BlockSize - BlockSize * 0.5f;

			phymat = new PhysicMaterial();
			phymat.staticFriction = 0.0f;
			phymat.dynamicFriction = 0.0f;
			phymat.bounciness = 1.0f;
			phymat.frictionCombine = PhysicMaterialCombine.Minimum;
			phymat.bounceCombine = PhysicMaterialCombine.Maximum;

			se = new AudioSource[4];
			for (int i = 0; i < se.Length; ++i)
			{
				var path = ("file:://" + RootDir + "/data/" + string.Format("{0:000}", i) + ".wav").Replace("\\", "/");
				WWW audioLoader = new WWW(path);
				while (!audioLoader.isDone) {; }
				se[i] = this.gameObject.AddComponent<AudioSource>();
				se[i].clip = audioLoader.GetAudioClip(false, false, AudioType.WAV);
			}

			material = new Material(Shader.Find("UI/Default"));
			material.renderQueue = 5000;

			blockMaterials = new Material[BlockMaterials.Count];
			for (int i = 0; i < BlockMaterials.Count; ++i)
			{
				blockMaterials[i] = new Material(Shader.Find("UI/Default"));
				blockMaterials[i].renderQueue = 5000;
				blockMaterials[i].color = BlockMaterials[i].Color;
			}

			// ブロック
			for (int y = 0; y < BlockNumY; ++y)
			{
				for (int x = 0; x < BlockNumX; ++x)
				{
					int id = BlockTable[y * BlockNumX + x] - 1;
					if (id >= 0)
					{
						var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
						block.name = "Block";
						block.transform.parent = this.gameObject.transform;
						block.transform.localPosition = new Vector3(l + x * BlockSize + BlockSize * 0.5f, u - y * BlockSize - BlockSize * 0.5f, 0.0f);
						block.transform.localScale = new Vector3(BlockSize, BlockSize, BlockThickness);
						block.renderer.sharedMaterial = blockMaterials[ id ];
						block.GetComponent<BoxCollider>().sharedMaterial = phymat;
						var blcomp = block.AddComponent<Block>();
						blcomp.life = BlockMaterials[id].Life;
						blcomp.mat = BlockMaterials[id];
					}
				}
			}

			// 壁
			float wallWidth = BlockSize * 0.25f;
			var wallL = GameObject.CreatePrimitive(PrimitiveType.Cube);
			wallL.name = "Wall";
			wallL.transform.parent = this.gameObject.transform;
			wallL.transform.localPosition = new Vector3(l - wallWidth * 0.5f, 0.0f);
			wallL.transform.localScale = new Vector3(wallWidth, BlockSize * (BlockNumY + 1), BlockThickness);
			wallL.renderer.sharedMaterial = material;
			wallL.GetComponent<BoxCollider>().sharedMaterial = phymat;

			var wallR = GameObject.CreatePrimitive(PrimitiveType.Cube);
			wallR.name = "Wall";
			wallR.transform.parent = this.gameObject.transform;
			wallR.transform.localPosition = new Vector3(r + wallWidth * 0.5f, 0.0f);
			wallR.transform.localScale = new Vector3(wallWidth, BlockSize * (BlockNumY + 1), BlockThickness);
			wallR.renderer.sharedMaterial = material;
			wallR.GetComponent<BoxCollider>().sharedMaterial = phymat;

			var wallU = GameObject.CreatePrimitive(PrimitiveType.Cube);
			wallU.name = "Wall";
			wallU.transform.parent = this.gameObject.transform;
			wallU.transform.localPosition = new Vector3(0.0f, u + wallWidth * 0.5f, 0.0f);
			wallU.transform.localScale = new Vector3(BlockSize * BlockNumX + wallWidth * 2.0f, wallWidth, BlockThickness);
			wallU.renderer.sharedMaterial = material;
			wallU.GetComponent<BoxCollider>().sharedMaterial = phymat;

			// パドル
			float paddleWidth = BlockSize * 2.0f;
			float paddleHeight = BlockSize * 0.5f;
			var paddle = GameObject.CreatePrimitive(PrimitiveType.Cube);
			paddle.name = "Paddle";
			paddle.transform.parent = this.gameObject.transform;
			paddle.transform.localPosition = new Vector3(0.0f, d + paddleHeight * 0.5f, 0.0f);
			paddle.transform.localScale = new Vector3(paddleWidth, paddleHeight, BlockThickness);
			paddle.renderer.sharedMaterial = material;
			paddle.GetComponent<BoxCollider>().sharedMaterial = phymat;
			var paddleComp = paddle.AddComponent<Paddle>();
			paddleComp.limitLeft = l;
			paddleComp.limitRight = r;
			paddleComp.paddleSpeed = BlockNumX * BlockSize * 0.5f;

			// ボール
			var ballRadius = BlockSize * 0.25f;
			var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			ball.name = "Ball";
			ball.transform.parent = this.gameObject.transform;
			ball.transform.localScale = new Vector3(ballRadius * 2.0f, ballRadius * 2.0f, ballRadius * 2.0f);
			ball.renderer.sharedMaterial = material;
			ball.GetComponent<SphereCollider>().sharedMaterial = phymat;
			var ballRb = ball.AddComponent<Rigidbody>();
			ballRb.useGravity = false;
			ballRb.mass = 10.0f;
			ballRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
			ballRb.constraints =
				RigidbodyConstraints.FreezeRotationX |
				RigidbodyConstraints.FreezeRotationY |
				RigidbodyConstraints.FreezeRotationZ;
			ballRb.drag = 0.0f;
			ballRb.angularDrag = 0.0f;
			var ballComp = ball.AddComponent<Ball>();
			ballComp.started = false;
			ballComp.left = l;
			ballComp.right = r;
			ballComp.up = u;
			ballComp.down = d;
			ballComp.speed = BallSpeed;
			ballComp.speedMinX = BallSpeed * 0.25f;
			ballComp.speedMinY = BallSpeed * 0.5f;
		}

		// ブロック
		public class Block : MonoBehaviour
		{
			public int life = 1;
			public BlockMaterial mat = null;
		}

		// パドル
		public class Paddle : MonoBehaviour
		{
			public float limitLeft = 0.0f;
			public float limitRight = 0.0f;
			public float paddleSpeed = 1.0f;

			void Update()
			{
				if (!ArkamaidMain.AutoPlay)
				{
					var speed = paddleSpeed;
					if (Input.GetKey(KeyCode.LeftShift)) speed = speed * 3.0f;
					if (Input.GetKey(KeyCode.LeftArrow)) this.gameObject.transform.localPosition += new Vector3(-speed, 0.0f, 0.0f) * Time.deltaTime;
					if (Input.GetKey(KeyCode.RightArrow)) this.gameObject.transform.localPosition += new Vector3(+speed, 0.0f, 0.0f) * Time.deltaTime;
				}
				else
				{
					this.gameObject.transform.localPosition = new Vector3(
						GameObject.Find("Ball").transform.localPosition.x,
						this.gameObject.transform.localPosition.y, this.gameObject.transform.localPosition.z);
				}

				if (Input.GetKeyDown(KeyAutoPlay))
				{
					ArkamaidMain.AutoPlay = !ArkamaidMain.AutoPlay;
				}

				var pos = this.gameObject.transform.localPosition;
				var l = limitLeft + this.transform.localScale.x * 0.5f;
				var r = limitRight - this.transform.localScale.x * 0.5f;
				if (pos.x < l) this.gameObject.transform.localPosition = new Vector3(l, pos.y, pos.z);
				if (pos.x > r) this.gameObject.transform.localPosition = new Vector3(r, pos.y, pos.z);
			}
		}

		// ボール
		public class Ball : MonoBehaviour
		{
			public bool started = false;
			public float left = 0.0f;
			public float right = 0.0f;
			public float up = 0.0f;
			public float down = 0.0f;
			public float speed = 1.0f;
			public float speedMinX = 1.0f;
			public float speedMinY = 1.0f;
			private int hitCount = 0;

			void Update()
			{
				if (!started)
				{
					var paddle = GameObject.Find("Paddle");
					this.gameObject.transform.localPosition = paddle.transform.localPosition + new Vector3(0.0f, this.transform.localScale.y + paddle.transform.localScale.y, 0.0f);

					if (Input.GetKeyDown(KeyStart) || Input.GetKeyDown(KeyAutoPlay))
					{
						var rb = this.gameObject.GetComponent<Rigidbody>();
						rb.velocity = this.gameObject.transform.TransformDirection(new Vector3(0.0f, -1.0f, 0.0f)) * speed;
						started = true;
						ArkamaidMain.AutoPlay = Input.GetKeyDown(KeyAutoPlay);
					}
				}
				else
				{
					if (this.gameObject.transform.localPosition.y < (down - this.gameObject.transform.localScale.y))
					{
						bool lastone = true;
						foreach (var gameObj in GameObject.FindObjectsOfType<GameObject>())
						{
							if (gameObj == this.gameObject) continue;
							if (gameObj.name == "Ball")
							{
								lastone = false;
								break;
							}
						}

						if (!lastone)
						{
							UnityEngine.Object.Destroy(this.gameObject);
						}
						else
						{
							se[3].Play();
							started = false;
							hitCount = 0;
						}
					}
				}

				this.gameObject.transform.localPosition = new Vector3(
					Mathf.Clamp(this.gameObject.transform.localPosition.x, left, right),
					Mathf.Min(this.gameObject.transform.localPosition.y, up), 0.0f);
			}

			void OnCollisionEnter(Collision c)
			{
				if (!started) return;
				
				// ブロック
				if (c.gameObject.name == "Block")
				{
					var b = c.gameObject.GetComponent<Block>();
					b.life--;
					if (b.life <= 0)
					{
						se[1].Play();
						UnityEngine.Object.Destroy(c.gameObject);
						ArkamaidMain.ShakeOffsetScale = 1.0f;
					}
					else
					{
						se[2].Play();
					}
				}

				// パドルとの衝突
				if (c.gameObject.name == "Paddle")
				{
					se[0].Play();

					hitCount++;
					if (hitCount >= 5)
					{
						hitCount = 0;

						for (int i = 0; i < 2; ++i)
						{
							var r = UnityEngine.Random.Range(-Mathf.PI, Mathf.PI);
							var nb = UnityEngine.Object.Instantiate(this.gameObject) as GameObject;
							nb.name = "Ball";
							nb.transform.parent = this.gameObject.transform.parent;
							nb.transform.rotation = this.gameObject.transform.rotation;
							nb.transform.position = this.gameObject.transform.position;
							nb.GetComponent<Rigidbody>().velocity = this.gameObject.transform.TransformDirection(
								new Vector3(Mathf.Sin(r), Mathf.Cos(r), 0.0f)) * speed;
						}
					}
				}

				// ボールとの衝突
				if (c.gameObject.name == "Ball")
				{
					se[2].Play();
				}

				// 速度を一定に保つ
				var rb = this.gameObject.GetComponent<Rigidbody>();

				rb.velocity -= this.gameObject.transform.forward * Vector3.Dot(this.gameObject.transform.forward, rb.velocity);
				rb.velocity = rb.velocity.normalized * speed;

				var doty = Vector3.Dot(this.gameObject.transform.up, rb.velocity);
				var magy = Mathf.Abs(doty);
				if (magy < speedMinY)
				{
					if (magy == 0.0f)
					{
						rb.velocity += this.gameObject.transform.up * (UnityEngine.Random.Range(0, 2) == 0 ? -speedMinY : speedMinY);
					}
					else
					{
						rb.velocity += this.gameObject.transform.up * Mathf.Sign(doty) * speedMinY;
					}
				}

				var dotx = Vector3.Dot(this.gameObject.transform.right, rb.velocity);
				var magx = Mathf.Abs(dotx);
				if (magx < speedMinX)
				{
					if (magx == 0.0f)
					{
						rb.velocity += this.gameObject.transform.right * (UnityEngine.Random.Range(0, 2) == 0 ? -speedMinX : speedMinX);
					}
					else
					{
						rb.velocity += this.gameObject.transform.right * Mathf.Sign(dotx) * speedMinX;
					}
				}
			}
		}
	}
}