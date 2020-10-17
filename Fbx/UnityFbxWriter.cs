using System.IO;
using UnityEngine;

namespace Fbx {

	public class UnityFbxWriter {
		/* consts */
		const string TEMPLATE_FILENAME = "FbxTemplate"; // add this to Resources dir, copy from FbxTemplate.bytes file in repository

		const string NODE_VERTICES = "Vertices";
		const string NODE_NORMALS = "Normals";
		const string NODE_POLYGON_VERTEX_INDEX = "PolygonVertexIndex";
		const string NODE_GEOMETRY = "Geometry";
		const string NODE_MODEL = "Model";

		public static void ExportToBinary(GameObject gameObject, string outputFilePath) {
			var meshFilter = gameObject.GetComponent<MeshFilter>();
			if (meshFilter == null) {
				Debug.LogWarning("No MeshFilter found on gameObject to export!");
				return;
			}

			var mesh = meshFilter.mesh;
			if (mesh == null) {
				Debug.LogWarning("No Mesh found on gameObject to export!");
				return;
			}

			var template = Resources.Load(TEMPLATE_FILENAME) as TextAsset;
			if (template == null || template.bytes == null || template.bytes.Length == 0) {
				Debug.LogWarning("No valid FbxTemplate.bytes file found in Resources. Please copy FbxTemplate.bytes (present in this repository) to your Unity project's Resources folder.");
				return;
			}

			var templateBytes = template.bytes;
			var templateStream = new MemoryStream(templateBytes);

			var doc = FbxIO.ReadBinary(templateStream);

			var verticesNode = FindNode(doc, NODE_VERTICES);
			var normalsNode = FindNode(doc, NODE_NORMALS);
			var polygonsNode = FindNode(doc, NODE_POLYGON_VERTEX_INDEX);

			var geometryNode = FindNode(doc, NODE_GEOMETRY);
			var modelNode = FindNode(doc, NODE_MODEL);

			if (verticesNode == null || normalsNode == null || polygonsNode == null || geometryNode == null || modelNode == null) {
				Debug.LogWarning("Invalid FbxTemplate.bytes file");
				return;
			}

			geometryNode.node.Properties[1] = NODE_GEOMETRY + "::" + gameObject.name;
			modelNode.node.Properties[1] = NODE_MODEL + "::" + gameObject.name;

			var vertices = mesh.vertices;
			var normalsPerVert = mesh.normals;
			var triIndices = mesh.triangles;

			var normals = new Vector3[triIndices.Length];

			for (int i = 0; i < triIndices.Length; i += 3) {
				// set normals
				var n1 = normalsPerVert[triIndices[i]];
				var n2 = normalsPerVert[triIndices[i + 2]];
				var n3 = normalsPerVert[triIndices[i + 1]];

				normals[i] = n1;
				normals[i + 1] = n2;
				normals[i + 2] = n3;

				// swap 2nd and 3rd, since Unity has inverted x in verts and normals
				var t = triIndices[i + 2];
				triIndices[i + 2] = triIndices[i + 1];
				triIndices[i + 1] = t;

				triIndices[i + 2] = ~triIndices[i + 2]; // Fbx spec: negative complement for triangle's last index
			}

			Update(verticesNode.node, Vec3ToDouble(vertices));
			Update(normalsNode.node, Vec3ToDouble(normals));
			Update(polygonsNode.node, triIndices);

			FbxIO.WriteBinary(doc, outputFilePath);
		}

		static double[] Vec3ToDouble(Vector3[] arr) {
			if (arr == null) {
				Debug.LogWarning("Array being converted is null");
				return null;
			}

			var newArr = new double[arr.Length * 3];
			for (int i = 0; i < arr.Length; i++) {
				var v = arr[i];
				newArr[3*i] = -v.x; // unity has inverted x
				newArr[3*i+1] = v.y;
				newArr[3*i+2] = v.z;
			}

			return newArr;
		}

		static void Update<T>(FbxNode node, T[] arr) {
			if (arr == null) {
				Debug.LogWarning("Array being assigned is null");
				return;
			}

			if (node.Value != null && node.Value.GetType() != arr.GetType()) {
				Debug.LogWarning("Array being assigned does not match the previous array's type in " + node.Name + "! Required: " + node.Value.GetType() + ", Got: " + arr.GetType());
				return;
			}

			node.Value = arr;
		}

		static NodeLink FindNode(FbxDocument doc, string name) {
            foreach (var node in doc.Nodes) {
                var foundNode = FindNode(node, name);
                if (foundNode != null) {
                    return foundNode;
                }
            }

            return null;
        }

        static NodeLink FindNode(FbxNode parent, string name) {
            foreach (var node in parent.Nodes) {
                if (node == null) {
                    continue;
                }

                if (node.Name == name) {
                    return new NodeLink(parent, node);
                }

                if (node.Nodes.Count > 0) {
                    var foundNode = FindNode(node, name);
                    if (foundNode != null) {
                        return foundNode;
                    }
                }
            }

            return null;
        }

		class NodeLink {
			public FbxNode parent;
			public FbxNode node;

			public NodeLink(FbxNode parent, FbxNode node) {
				this.parent = parent;
				this.node = node;
			}
		}
	}

}