
namespace StepManiaEditor
{
	/// <summary>
	/// Interface for 2d object which can be placed at an X,Y position with a width and height.
	/// </summary>
	public interface IPlaceable
	{
		public double X { get; set; }
		public double Y { get; set; }
		public double W { get; set; }
		public double H { get; set; }
	}
}
