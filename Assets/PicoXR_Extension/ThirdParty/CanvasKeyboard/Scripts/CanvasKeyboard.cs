// Modded by JCxYIS
// 20221227

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
// using System.Reflection;
using UnityEngine.EventSystems;

namespace TalesFromTheRift
{
	public class CanvasKeyboard : MonoBehaviour 
	{
		#region CanvasKeyboard Instantiation

		public enum CanvasKeyboardType
		{
			ASCIICapable
		}
		
		private static CanvasKeyboard Instance;

		public static void Open(InputField input = null, CanvasKeyboardType keyboardType = CanvasKeyboardType.ASCIICapable)
		{
			if(input == inputObject)
			{
				// Debug.Log("[CanvasKeyboard] No need to reopen");
				return;
			}

			if(!Instance || !Instance.gameObject)
			{
				Instance = GameObject.FindObjectOfType<CanvasKeyboard>();
			}						
			Instance.OpenKeyboard(input);
			Debug.Log("[CanvasKeyboard] Opened");
		}
		
		public static void Close()
		{
			if(!Instance || !Instance.gameObject)
			{
				Instance = GameObject.FindObjectOfType<CanvasKeyboard>();
			}
			Instance.CloseKeyboard();			
			Debug.Log("[CanvasKeyboard] Closed");
		}

		public static void Close(InputField currentInputObject = null)
		{
			if(currentInputObject == inputObject)
				Close();
		}
		
		public static bool IsOpen 
		{
			get
			{
				// return GameObject.FindObjectsOfType<CanvasKeyboard>().Length != 0;
				return Instance?.isActiveAndEnabled ?? false;
			}
		}

		#endregion

		// public GameObject inputObject;
		public static InputField inputObject;


		public string text { get => inputObject.text; set => inputObject.text=value; }
		// {
		// 	get
		// 	{
		// 		if (inputObject != null) 
		// 		{
		// 			Component[] components = inputObject.GetComponents(typeof(Component));
		// 			foreach (Component component in components)
		// 			{
		// 				PropertyInfo prop = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
		// 				if (prop != null)
		// 				{
		// 					return prop.GetValue(component, null)  as string;;
		// 				}
		// 			}
		// 			return inputObject.name;
		// 		}
		// 		return "";
		// 	}
			
		// 	set 
		// 	{
		// 		if (inputObject != null) 
		// 		{
		// 			Component[] components = inputObject.GetComponents(typeof(Component));
		// 			foreach (Component component in components)
		// 			{
		// 				PropertyInfo prop = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
		// 				if (prop != null)
		// 				{
		// 					prop.SetValue(component, value, null);
		// 					return;
		// 				}
		// 			}
		// 			inputObject.name = value;
		// 		}
		// 	}
		// }

		#region Keyboard Receiving Input

		public void SendKeyString(string keyString)
		{
			if (keyString.Length == 1 && keyString[0] == 8/*ASCII.Backspace*/)
			{
				if (text.Length > 0)
				{
					text = text.Remove(text.Length - 1); 
				}
			}
			else
			{
				text += keyString;
			}

			// Workaround: Restore focus to input fields (because Unity UI buttons always steal focus)
			ReactivateInputField(inputObject.GetComponent<InputField>());

		}

		public void OpenKeyboard(InputField input)
		{
			inputObject = input;
			gameObject.SetActive(true);
		}

		public void CloseKeyboard()
		{
			// Destroy(gameObject);
			gameObject.SetActive(false);
			inputObject = null;
		}

		#endregion


		#region Steal Focus Workaround

		void ReactivateInputField(InputField inputField)
		{
			if (inputField != null)
			{
				StartCoroutine(ActivateInputFieldWithoutSelection(inputField));
			}
		}

		IEnumerator ActivateInputFieldWithoutSelection(InputField inputField)
		{
			inputField.ActivateInputField();

			// wait for the activation to occur in a lateupdate
			yield return new WaitForEndOfFrame();

			// make sure we're still the active ui
			if (EventSystem.current.currentSelectedGameObject == inputField.gameObject)
			{
				// To remove hilight we'll just show the caret at the end of the line
				inputField.MoveTextEnd(false);
			}
		}

		#endregion

	}
}