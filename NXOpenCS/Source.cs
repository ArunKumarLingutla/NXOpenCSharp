﻿using NXOpen.UF;
using NXOpen;
using System;
using System.Collections.Generic;
using NXOpenUI;
using NXOpen.Assemblies;
using System.Linq;
using NXOpen.Annotations;
using NXOpen.Features;
using static NXOpen.BodyDes.OnestepUnformBuilder;
using static NXOpen.Tooling.ScrapDesignBuilder;
using Body = NXOpen.Body;
using Part = NXOpen.Part;

namespace NXOpenCS
{
    public class Source
    {
        //Taking NXSession, and collecting work and display parts from NXSession
        public static Session theSession;
        public static Part theWorkPart;
        public static Part theDisplayPart;
        public static ListingWindow lw;
        public static UFSession theUFSession;
        public static DisplayManager theDisplayManager;
        public static UI theUI;

        //Collecting Bodies, Faces, and Edges and storing them in the below lists
        public static List<Body> lsBodies=new List<Body>();
        public static List<Face> lsFaces=new List<Face>();
        public static List<Edge> lsEdgesFromBodies=new List<Edge>(); //will give exact no of edges without duplicate

        public static Line line;

        public Source()
        {
            //Initialising the session and parts from NX when ever Object is created for source class
            theSession = NXOpen.Session.GetSession();
            theWorkPart = theSession.Parts.Work;
            theDisplayPart = theSession.Parts.Display;
            theDisplayManager = theSession.DisplayManager;
            lw = theSession.ListingWindow;
            lw.Open();
            theUFSession = NXOpen.UF.UFSession.GetUFSession();
            theUI = NXOpen.UI.GetUI();
        }

        public static void GetAllComponents()
        {
            //Get all parts open in the session
            //If it is an assembly, it gets all the components along with assembly, if ypu have 2 parts in an assembly it will give 3
            PartCollection partList=theSession.Parts;
            lw.WriteLine(Convert.ToString(partList.ToArray().Length));

            //Getting all the sub assemblies and components in the assembly
            List<NXOpen.Assemblies.Component> assemblies = new List<Component>(); 
            List<NXOpen.Assemblies.Component> components = new List<Component>(); 

            try
            {
                //If the display part is not an assembly than root component will be null
                if (theDisplayPart.ComponentAssembly.RootComponent is null)
                {
                    lw.WriteLine("It is not an assembly");
                }
                else 
                { 
                    NXOpen.Assemblies.Component rootComponent=theDisplayPart.ComponentAssembly.RootComponent;

                    //Going layer by level by level and getting all the child components
                    //storing current level components into "currentLevelComponents" list and traversing them to get their child components
                    //storing the child components in allChildComponents and making those child components into current level components and continuing the process
                    List<Component> currentLevelComponents=rootComponent.GetChildren().ToList();
                    List<Component> allChildComponents = rootComponent.GetChildren().ToList();
                    while (true) 
                    { 
                        //Getting child components of current level and adding it to allChildComponents, if there are no child components loop terminates
                        List<Component> childComponentsOfCurrentLevelComponents=currentLevelComponents.SelectMany(x => x.GetChildren()).ToList();
                        if(childComponentsOfCurrentLevelComponents.Count==0) break;
                        allChildComponents.AddRange(childComponentsOfCurrentLevelComponents);
                        currentLevelComponents=childComponentsOfCurrentLevelComponents;
                    }

                    //saparating assemblies and individual components from allChildComponents
                    assemblies=allChildComponents.Where(x=>x.GetChildren().Length!=0).ToList();
                    components=allChildComponents.Where(x=>x.GetChildren().Length==0).ToList();

                    lw.WriteLine("Assemblies in "+rootComponent.DisplayName+" are: ");
                    foreach (Component component in assemblies)
                    {
                        lw.WriteLine(component.DisplayName);
                    }

                    lw.WriteLine("Components in " + rootComponent.DisplayName + " are: ");
                    foreach (Component component in components)
                    {
                        lw.WriteLine(component.DisplayName);
                    }
                }
            }
            catch (Exception ex)
            {
                theUI.NXMessageBox.Show("Error", NXMessageBox.DialogType.Error, Convert.ToString(ex.Message));
            }
        }

        public static void GetBodiesFacesEdges()
        {
            try
            {
                //BodyCollection bodies = theWorkPart.Bodies;

                Body[] bodiesArray = theWorkPart.Bodies.ToArray();
                lsBodies.AddRange(bodiesArray);

                foreach (Body body in bodiesArray)
                {
                    lsFaces.AddRange(body.GetFaces());
                    lsEdgesFromBodies.AddRange(body.GetEdges());
                }

                theUI.NXMessageBox.Show("Out put", NXMessageBox.DialogType.Information, 
                    "No of bodies: "+Convert.ToString(lsBodies.Count) +
                    "No of Faces: "+ Convert.ToString(lsFaces.Count)+
                    "No of Edges"+ Convert.ToString(lsEdgesFromBodies.Count)
                );
            }
            catch (Exception ex) { 
                theUI.NXMessageBox.Show("Error", NXMessageBox.DialogType.Error, Convert.ToString(ex.Message));
            }
        }

        public void CreateLine()
        {
            Point3d startPoint = new Point3d(0, 0, 0);
            Point3d endPoint = new Point3d(0,100,100);
            line = theWorkPart.Curves.CreateLine(startPoint, endPoint);
            line.SetVisibility(SmartObject.VisibilityOption.Visible);
        }

        public void CreatePointPerpendicularToLine()
        {
            double[] startPoint = { line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z };
            double[] endPoint = { line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z };

            double[] midPt = {
                (startPoint[0] + endPoint[0]) / 2,
                (startPoint[1] + endPoint[1]) / 2,
                (startPoint[2] + endPoint[2]) / 2
            };

            //Point midPoint = theWorkPart.Points.CreatePoint(new Point3d(
            //(line.EndPoint.X + line.StartPoint.X) / 2,
            //(line.EndPoint.Y + line.StartPoint.Y) / 2,
            //(line.EndPoint.Z + line.StartPoint.Z) / 2)
            //);

            Point midPoint = theWorkPart.Points.CreatePoint(new Point3d(midPt[0], midPt[1], midPt[2]));
            midPoint.SetVisibility(SmartObject.VisibilityOption.Visible);

            // Calculate the direction vector of the line
            double[] lineVector = {
                (line.EndPoint.X - line.StartPoint.X), 
                (line.EndPoint.Y - line.StartPoint.Y), 
                (line.EndPoint.Z - line.StartPoint.Z) 
            };

            //The direction vector we calculated above has a magnitude (or length) based on the distance between the start and end points.
            //However, to work with directions without scaling, we often use a unit vector—a vector with a magnitude of 1 that points in the same direction.
            //to get unit vector, divide each component of vector with length
            double length = Math.Sqrt(
                (lineVector[0] * lineVector[0])+ 
                (lineVector[1] * lineVector[1])+ 
                (lineVector[2] * lineVector[2])
            );

            double[] unitVector = {
                lineVector[0]/length,
                lineVector[1]/length,
                lineVector[2]/length,
            };

            //Let us assume that it is in XY plane to reduce complexity, now the perpendicular to (X,Y,0) will be(-Y,X,0)
            //If we need it in YZ plane, perpendicular to (0,Y,Z) will be (0,-Z,Y)
            double[] perpendicularVector = {
                -unitVector[1],
                unitVector[0],
                0
            };

            //Move the mid point perpendicular direction with distance 10
            double distance = 10;
            double[] movedPt = {
                midPt[0]+perpendicularVector[0]*distance,
                midPt[1]+perpendicularVector[1]*distance,
                midPt[2]+perpendicularVector[2]*distance
            };

            Point movedPoint = theWorkPart.Points.CreatePoint(new Point3d(movedPt[0], movedPt[1], movedPt[2]));
            movedPoint.SetVisibility(SmartObject.VisibilityOption.Visible);

            CreatingPlanes(movedPoint);
        }

        public void CreateLinePerpendicularToExistingLine()
        {
            
            //In CreatePointPerpendicularToLine method we used double[] array to store coordinates and vectors,
            //so here we try to use point3d and vector3d to store coordinates and vectors

            //Calculating Mid Point from start and end points, by adding and dividing the corresponding components by 2
            Point3d startPoint =line.StartPoint;
            Point3d endPoint = line.EndPoint;

            Point3d midPoint =new Point3d( 
                (startPoint.X + endPoint.X) / 2,
                (startPoint.Y + endPoint.Y) / 2,
                (startPoint.Z + endPoint.Z) / 2
            );

            //calculating direction vector of original line, we can use double[] array or vector3D to store the direction vector
            Vector3d directionVectorOfOriginalLine = new Vector3d(
                endPoint.X - startPoint.X,
                endPoint.Y - startPoint.Y,
                endPoint.Z - startPoint.Z

            );

            //Calculate magnitude or length of the original line to calculate unit vector
            double length = Math.Sqrt(
                (directionVectorOfOriginalLine.X* directionVectorOfOriginalLine.X)+ 
                (directionVectorOfOriginalLine.Y * directionVectorOfOriginalLine.Y)+ 
                (directionVectorOfOriginalLine.Z * directionVectorOfOriginalLine.Z)
            );

            //unit vector of original line
            Vector3d unitVectorOfOriginalLine = new Vector3d(
                directionVectorOfOriginalLine.X/length,
                directionVectorOfOriginalLine.Y/length,
                directionVectorOfOriginalLine.Z/length
            );

            //Perpendicuar direction vector, let us assume it on YZ plane,
            //perpendicular direction vector for (0,Y,Z) is (0,-Z,Y)
            Vector3d unitVectorOfNewLine = new Vector3d(
                0,
                -unitVectorOfOriginalLine.Z,
                unitVectorOfOriginalLine.Y
            );

            //Perpendicular end point for new line
            double lengthOfNewLine = 30;
            Point3d perpendicularEndPointForNewLine = new Point3d(
                midPoint.X+unitVectorOfNewLine.X*lengthOfNewLine,
                midPoint.Y+unitVectorOfNewLine.Y*lengthOfNewLine,
                midPoint.Z+unitVectorOfNewLine.Z*lengthOfNewLine
            );

            Line newPerpendicularLine = theWorkPart.Curves.CreateLine(midPoint,perpendicularEndPointForNewLine);
        }

        public void CreatingPlanes(Point startPoint)
        {
            //For creating a plane we need origin(Point3d) and Matrix3X3
            //Creating plane along XY Plane normal to Z axis
            //In this case Matrix components will be ->Xaxis(1,0,0)    Yaxis(0,1,0)    Zaxis(0,0,1)
            //along YZ plane Normal to X axis       -> Xaxis(0,0,1)    Yaxis(0,1,0)    Zaxis(1,0,0)
            //along ZX plane Normal to Y axis       -> Xaxis(1,0,0)    Yaxis(0,0,1)    Zaxis(0,1,0)

            //Lets create a plane via a point which is create on above method, along XY plane by taking workPart matrix
            Matrix3x3 wcsMatrix = theWorkPart.WCS.CoordinateSystem.Orientation.Element;  //This will collect Matrix along XY
            Point3d origin=new Point3d(startPoint.Coordinates.X,startPoint.Coordinates.Y,startPoint.Coordinates.Z);
            DatumPlane sketchPlane=theWorkPart.Datums.CreateFixedDatumPlane(origin, wcsMatrix);

            lw.WriteLine(("Xx = "+wcsMatrix.Xx, "Xy = " + wcsMatrix.Xy, "Xz = " + wcsMatrix.Xz).ToString());
            lw.WriteLine(("Yx = "+wcsMatrix.Yx, "Yy = " + wcsMatrix.Yy, "Yz = " + wcsMatrix.Yz).ToString());
            lw.WriteLine(("Zx = "+wcsMatrix.Zx, "Zy = " + wcsMatrix.Zy, "Zz = " + wcsMatrix.Zz).ToString());

            //Creating plane via point, along XZ axis and normal to Y
            Matrix3x3 xzMatrix=new Matrix3x3();
            xzMatrix.Xx=1; xzMatrix.Xy=0; xzMatrix.Xz=0;
            xzMatrix.Yx=0; xzMatrix.Yy=0;xzMatrix.Yz=1;
            xzMatrix.Zx=0;xzMatrix.Zy=1;xzMatrix.Zz=0;

            DatumPlane xzPlane = theWorkPart.Datums.CreateFixedDatumPlane(origin,xzMatrix);


            //Creating a plane with some angle
            // Calculate components of the normal vector
            //CreateFixedDatumPlane:
                // Use when you need a persistent and reusable reference plane for building features.
                //Example: Creating a sketch plane that stays in the part's feature tree.
            //CreateFixedPlane:
                // Use when you need a temporary plane for programmatic calculations or intermediate steps.
                //Example: Finding where a plane intersects a body without adding a datum plane to the model.
            
            Point3d originOriginal=new Point3d(0,0,0);
            double angleInRadians = 53 * (Math.PI / 180.0); // Convert degrees to radians
            double normalZ = Math.Sin(angleInRadians); // Z-component
            double normalXY = Math.Cos(angleInRadians); // Length in XY plane

            // 1. Plane normal to Z (tilted 53 degrees to XY)
            Matrix3x3 matrixZ = new Matrix3x3();
            matrixZ.Xx = normalXY; matrixZ.Xy = 0.0; matrixZ.Xz = -normalZ; // X-axis direction
            matrixZ.Yx = 0.0; matrixZ.Yy = 1.0; matrixZ.Yz = 0.0;          // Y-axis direction
            matrixZ.Zx = normalZ; matrixZ.Zy = 0.0; matrixZ.Zz = normalXY; // Z-axis direction (normal)

            Plane planeZ = theWorkPart.Planes.CreateFixedPlane(originOriginal, matrixZ);

            // 2. Plane normal to X (tilted 53 degrees to YZ)
            Matrix3x3 matrixX = new Matrix3x3();
            matrixX.Xx = 1.0; matrixX.Xy = 0.0; matrixX.Xz = 0.0;          // X-axis direction
            matrixX.Yx = 0.0; matrixX.Yy = normalXY; matrixX.Yz = normalZ; // Y-axis direction
            matrixX.Zx = 0.0; matrixX.Zy = -normalZ; matrixX.Zz = normalXY; // Z-axis direction (normal)

            Plane planeX = theWorkPart.Planes.CreateFixedPlane(originOriginal, matrixX);

            // 3. Plane normal to Y (tilted 53 degrees to ZX)
            Matrix3x3 matrixY = new Matrix3x3();
            matrixY.Xx = normalXY; matrixY.Xy = normalZ; matrixY.Xz = 0.0; // X-axis direction
            matrixY.Yx = -normalZ; matrixY.Yy = normalXY; matrixY.Yz = 0.0; // Y-axis direction
            matrixY.Zx = 0.0; matrixY.Zy = 0.0; matrixY.Zz = 1.0;          // Z-axis direction (normal)

            Plane planeY = theWorkPart.Planes.CreateFixedPlane(originOriginal, matrixY);
        }
    }
}
