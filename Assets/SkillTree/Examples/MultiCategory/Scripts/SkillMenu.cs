﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace Adnc.SkillTree.Example.MultiCategory {
	public class SkillMenu : MonoBehaviour {
		public static SkillMenu current; // @TODO We probably can't have a static reference since it will cause issues

		Dictionary<SkillCollectionBase, SkillNode> nodeRef;
		List<SkillNode> skillNodes;

		[SerializeField] SkillTreeBase skillTree;

		[Header("Header")]
		[SerializeField] Transform categoryContainer;
		[SerializeField] GameObject categoryButtonPrefab;
		[SerializeField] Text skillOutput;
		[SerializeField] Text categoryName;

		[Header("Nodes")]
		[SerializeField] Transform nodeContainer;
		[SerializeField] GameObject nodeRowPrefab;
		[SerializeField] GameObject nodePrefab;
		[SerializeField] Color colorUnlock;
		[SerializeField] Color colorPurchase;
		[SerializeField] Color colorLock;

		[Header("Node Lines")]
		[SerializeField] Transform lineContainer;
		[SerializeField] GameObject linePrefab;

		[Header("Context Sidebar")]
		[SerializeField] RectTransform sidebarContainer;
		[SerializeField] Text sidebarTitle;
		[SerializeField] Text sidebarBody;
		[SerializeField] Text sidebarRequirements;
		[SerializeField] Button sidebarPurchase;

		void Awake () {
			current = this;
		}

		void Start () {
			SkillCategoryBase[] skillCategories = skillTree.GetCategories();

			// Clear out test categories
			foreach (Transform child in categoryContainer) {
				Destroy(child.gameObject);
			}

			// Populate categories
			foreach (SkillCategoryBase category in skillCategories) {
				GameObject go = Instantiate(categoryButtonPrefab);
				go.transform.SetParent(categoryContainer);
				go.transform.localScale = Vector3.one;
				
				Text txt = go.GetComponentInChildren<Text>();
				txt.text = category.displayName;

				// Dump in a tmp variable to force capture the variable by the event
				SkillCategoryBase tmpCat = category; 
				go.GetComponent<Button>().onClick.AddListener(() => {
					ShowCategory(tmpCat);
				});
			}

			if (skillCategories.Length > 0) {
				ShowCategory(skillCategories[0]);
			}
		}

		void ShowCategory (SkillCategoryBase category) {
			skillNodes = new List<SkillNode>();
			nodeRef = new Dictionary<SkillCollectionBase, SkillNode>();
			categoryName.text = string.Format("{0}: Level {1}", category.displayName, category.skillLv);
			ClearDetails();

			foreach (Transform child in nodeContainer) {
				Destroy(child.gameObject);
			}

			// Generate node row data
			List<List<SkillCollectionBase>> rows = new List<List<SkillCollectionBase>>();
			List<SkillCollectionBase> rootNodes = category.GetRootSkillCollections();
			rows.Add(rootNodes);
			RecursiveRowAdd(rows);

			// Output proper rows and attach data
			foreach (List<SkillCollectionBase> row in rows) {
				GameObject nodeRow = Instantiate(nodeRowPrefab);
				nodeRow.transform.SetParent(nodeContainer);
				nodeRow.transform.localScale = Vector3.one;
				
				foreach (SkillCollectionBase rowItem in row) {
					GameObject node = Instantiate(nodePrefab);
					node.transform.SetParent(nodeRow.transform);
					node.transform.localScale = Vector3.one;

					SkillNode skillNode = node.GetComponent<SkillNode>();
					skillNode.skillCollection = rowItem;
					skillNodes.Add(skillNode);

					nodeRef[rowItem] = skillNode;

					node.GetComponentInChildren<Text>().text = rowItem.displayName;
				}
			}

			StartCoroutine(ConnectNodes());
		}

		// @TODO Instead loop through and update the status of all nodes with an enum, then visually update
		void UpdateNodes () {
			foreach (SkillNode node in skillNodes) {
				Button btn = node.GetComponent<Button>();
				ColorBlock color = btn.colors;

				if (node.skillCollection.Skill.unlocked) {

					color.normalColor = colorUnlock;
					color.highlightedColor = colorUnlock;
				
				} else if (skillTree.skillPoints > 0 && node.skillCollection.Skill.IsRequirements()) {

					color.normalColor = colorLock;
					color.highlightedColor = colorLock;

					// Verify one parent node has at least one skill unlocked
					if (node.parents.Count > 0) {
						foreach (SkillNode parent in node.parents) {
							if (parent.skillCollection.SkillIndex > 0) {
								Debug.Log(parent);
								color.normalColor = colorPurchase;
								color.highlightedColor = colorPurchase;
								break;
							}
						}
					} else {
						color.normalColor = colorPurchase;
						color.highlightedColor = colorPurchase;
					}

				} else {

					color.normalColor = colorLock;
					color.highlightedColor = colorLock;

				}

				btn.colors = color;
			}
		}

		// Done after a frame skip so they nodes are sorted properly into position
		IEnumerator ConnectNodes () {
			bool skipFrame = true;

			if (skipFrame) {
				skipFrame = false;
				yield return null;
			}

			// Generate draw lines between each node and populate parent / child relationships
			foreach (SkillNode node in skillNodes) {
				foreach (SkillCollectionBase child in node.skillCollection.childSkills) {
					node.children.Add(nodeRef[child]);
					nodeRef[child].parents.Add(node);
					DrawLine(lineContainer, node.transform.position, nodeRef[child].transform.position);
				}
			}

			Repaint();
		}

		void DrawLine (Transform container, Vector3 start, Vector3 end) {
			GameObject go = Instantiate(linePrefab);
			go.transform.localScale = Vector3.one;

			// Adjust the layering so it appears underneath
			go.transform.SetParent(container);
			go.transform.SetSiblingIndex(0);

			// Adjust height to proper sizing
			RectTransform rectTrans = go.GetComponent<RectTransform>();
			Rect rect = rectTrans.rect;
			rect.height = Vector3.Distance(start, end);
			rectTrans.sizeDelta = new Vector2(rect.width, rect.height);

			// Adjust rotation and placement
			go.transform.rotation = Helper.Rotate2D(start, end);
			go.transform.position = start;
		}

		void RecursiveRowAdd (List<List<SkillCollectionBase>> rows) {
			List<SkillCollectionBase> row = new List<SkillCollectionBase>();
			foreach (SkillCollectionBase collection in rows[rows.Count - 1]) {
				foreach (SkillCollectionBase child in collection.childSkills) {
					// @TODO We need to remove any duplicate entries (keep a record of every node added for ref)
					// As an entry might leak through as a deeper child node later down the tree
					if (!row.Contains(child)) {
						row.Add(child);
					}
				}
			}

			if (row.Count > 0) {
				rows.Add(row);
				RecursiveRowAdd(rows);
			}
		}

		public void ShowNodeDetails (SkillCollectionBase skillCollection) {
			sidebarTitle.text = skillCollection.displayName;
			sidebarBody.text = skillCollection.Skill.description;

			string requirements = skillCollection.Skill.GetRequirements();
			if (string.IsNullOrEmpty(requirements)) {
				sidebarRequirements.text = "";
			} else {
				sidebarRequirements.text = "<b>Requirements:</b> \n" + skillCollection.Skill.GetRequirements();
			}

			sidebarContainer.gameObject.SetActive(true);
		}

		void ClearDetails () {
			sidebarContainer.gameObject.SetActive(false);
		}

		void Repaint () {
			skillOutput.text = "Skill Points: " + skillTree.skillPoints;

			// @TODO Update tree node display status
			UpdateNodes();
		}

		void OnDestroy () {
			current = null;
		}
	}
}
