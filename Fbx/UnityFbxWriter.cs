using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx {

	public class UnityFbxWriter {
		/* extern */
		public static string APPLICATION_VENDOR = "";
		public static string APPLICATION_NAME = "";
		public static string APPLICATION_CREATOR = "";
		public static string APPLICATION_VERSION = "";

		/* consts */
		const string TEMPLATE_FILENAME = "FbxTemplate"; // add this to Resources dir, copy from FbxTemplate.bytes file in repository
		const string TEMPLATE_FILENAME_FULL = TEMPLATE_FILENAME + ".bytes";

		const string NODE_VERTICES = "Vertices";
		const string NODE_NORMALS = "Normals";
		const string NODE_POLYGON_VERTEX_INDEX = "PolygonVertexIndex";
		const string NODE_GEOMETRY = "Geometry";
		const string NODE_MODEL = "Model";
		const string NODE_CREATOR = "Creator";
		const string NODE_PROPERTIES70 = "Properties70";
		const string NODE_P = "P";
		const string NODE_APPLICATION_VENDOR = "ApplicationVendor";
		const string NODE_APPLICATION_NAME = "ApplicationName";
		const string NODE_APPLICATION_VERSION = "ApplicationVersion";

		public static void ExportToBinary(GameObject gameObject, string outputFilePath) {
			/* sanity checks */
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
				Debug.LogWarning("No valid " + TEMPLATE_FILENAME_FULL + " file found in Resources. Please copy " + TEMPLATE_FILENAME_FULL + " (present in this repository) to your Unity project's Resources folder.");
				return;
			}

			if (string.IsNullOrEmpty(APPLICATION_VENDOR)) {
				APPLICATION_VENDOR = Application.companyName;
			}
			if (string.IsNullOrEmpty(APPLICATION_CREATOR)) {
				APPLICATION_CREATOR = Application.productName;
			}
			if (string.IsNullOrEmpty(APPLICATION_NAME)) {
				APPLICATION_NAME = Application.productName;
			}
			if (string.IsNullOrEmpty(APPLICATION_VERSION)) {
				APPLICATION_VERSION = Application.version;
			}

			/* read and parse template */
			var templateBytes = template.bytes;
			var templateStream = new MemoryStream(templateBytes);

			var doc = FbxIO.ReadBinary(templateStream);

			var verticesNode = FindNode(doc, NODE_VERTICES);
			var normalsNode = FindNode(doc, NODE_NORMALS);
			var polygonsNode = FindNode(doc, NODE_POLYGON_VERTEX_INDEX);

			var geometryNode = FindNode(doc, NODE_GEOMETRY);
			var modelNode = FindNode(doc, NODE_MODEL);

			if (verticesNode == null || normalsNode == null || polygonsNode == null || geometryNode == null || modelNode == null) {
				Debug.LogWarning("Invalid " + TEMPLATE_FILENAME_FULL + " file");
				return;
			}

			/* set vendor and owner */
			var creator = FindNodes(doc, NODE_CREATOR);
			foreach (var c in creator) {
				c.node.Value = APPLICATION_CREATOR;
			}

			var p70 = FindNode(doc, NODE_PROPERTIES70);
			var pNodes = FindNodes(p70.node, NODE_P);
			foreach (var p in pNodes) {
				if (p.node.Properties[0].ToString().Contains(NODE_APPLICATION_VENDOR)) {
					p.node.Properties[4] = APPLICATION_VENDOR;
				}
				if (p.node.Properties[0].ToString().Contains(NODE_APPLICATION_NAME)) {
					p.node.Properties[4] = APPLICATION_NAME;
				}
				if (p.node.Properties[0].ToString().Contains(NODE_APPLICATION_VERSION)) {
					p.node.Properties[4] = APPLICATION_VERSION;
				}
			}

			/* set new model data */
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

		static NodeLink FindNode(FbxNodeList parent, string name) {
			var l = FindNodes(parent, name);
			if (l == null || l.Count == 0) {
				return null;
			}

			return l[0];
		}

		static List<NodeLink> FindNodes(FbxNodeList parent, string name) {
			var list = new List<NodeLink>();

			foreach (var node in parent.Nodes) {
				if (node == null) {
					continue;
				}

				if (node.Name == name) {
					list.Add(new NodeLink(parent, node));
				}

				if (node.Nodes.Count > 0) {
					var foundNodes = FindNodes(node, name);
					if (foundNodes != null) {
						list.AddRange(foundNodes);
					}
				}
			}

			return list;
		}

		class NodeLink {
			public FbxNodeList parent;
			public FbxNode node;

			public NodeLink(FbxNodeList parent, FbxNode node) {
				this.parent = parent;
				this.node = node;
			}
		}
	}

}