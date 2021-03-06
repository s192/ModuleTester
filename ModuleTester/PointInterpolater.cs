using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using acadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using System;


namespace HSKDIProject
{
    public class PointInterpolater
    {
        [CommandMethod("InterpolatePoint")]
        public static void InterpolatePoint()
        {
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            PromptEntityOptions peoXref = new PromptEntityOptions("\nSelect Grading Xref.")
            {
                AllowObjectOnLockedLayer = true,
                AllowNone = false
            };
            PromptEntityOptions peoPolyLine1 = new PromptEntityOptions("\nSelect polyline 1.");
            PromptEntityOptions peoPolyLine2 = new PromptEntityOptions("\nSelect polyline 2.");
            PromptPointOptions ppo = new PromptPointOptions("\nSelect Point of intrest.");

            PromptPointResult ppr = ed.GetPoint(ppo);
            if (ppr.Status == PromptStatus.OK)
            {
                Transaction tr = doc.TransactionManager.StartTransaction();
                using (tr)
                {
                    try
                    {
                        Point3d pt = ppr.Value;
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        PromptEntityResult perXref = ed.GetEntity(peoXref);
                        if (perXref.Status == PromptStatus.OK)
                        {
                            BlockReference xrefRef = (BlockReference)tr.GetObject(perXref.ObjectId, OpenMode.ForRead);
                            if (xrefRef != null)
                            {
                                // If so, we check whether the block table record to which it refers is actually from an XRef                         

                                ObjectId xrefId = xrefRef.BlockTableRecord;
                                BlockTableRecord xrefBTR = (BlockTableRecord)tr.GetObject(xrefId, OpenMode.ForRead);
                                if (xrefBTR != null)
                                {
                                    if (xrefBTR.IsFromExternalReference)
                                    {
                                        // If so, then we prigrammatically select the object underneath the pick-point already used                                    
                                        PromptNestedEntityOptions pneo = new PromptNestedEntityOptions("")
                                        {
                                            NonInteractivePickPoint = perXref.PickedPoint,
                                            UseNonInteractivePickPoint = true
                                        };
                                        PromptNestedEntityResult pner = ed.GetNestedEntity(pneo);
                                        if (pner.Status == PromptStatus.OK)
                                        {
                                            try
                                            {
                                                ObjectId selId = pner.ObjectId;

                                                // Let's look at this programmatically-selected object, to see what it is
                                                DBObject obj = tr.GetObject(selId, OpenMode.ForRead);

                                                // If it's a polyline vertex, we need to go one level up to the polyline itself

                                                if (obj is PolylineVertex3d || obj is Vertex2d) selId = obj.OwnerId;

                                                // We don't want to do anything at all for textual stuff, let's also make sure we are 
                                                // dealing with an entity (should always be the case)

                                                if (obj is MText || obj is DBText || !(obj is Entity)) return;

                                                // Now let's get the name of the layer, to use later

                                                Entity ent = (Entity)obj;
                                                LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);

                                                ed.WriteMessage("\nObject Selected is {0} on layer {1} in xref {2}.", selId.GetType(), ltr.Name, xrefBTR.Name);
                                            }
                                            catch
                                            {
                                                // A number of innocuous things could go wrong
                                                // so let's not worry about the details

                                                // In the worst case we are simply not trying
                                                // to replace the entity, so OFFSET will just
                                                // reject the selected Xref
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        ed.WriteMessage("\nFailed in xrefSelect");
                    }
                    //BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                }
                tr.Commit();
            }
            else
            {
                ed.WriteMessage("Failed to find point of intrest.");
            }
        }

        public static Point3d InterpolatePoint(Polyline A, Polyline B, Point3d p, Matrix3d ucs)
        {
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            Point3d a = HSKDICommon.Commands.ClosestPtOnSegment(p, A, ucs);
            Point3d b = HSKDICommon.Commands.ClosestPtOnSegment(p, B, ucs);

            double XYdist_a_p = Math.Abs(a.X - p.X) + Math.Abs(a.Y - p.Y);
            double XYdist_a_b = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
            double xyDistPercentageFromA = XYdist_a_p / XYdist_a_b;

            double c_Z = a.Z + xyDistPercentageFromA * Math.Abs(a.Z - b.Z);

            Point3d c = new Point3d(p.X, p.Y, c_Z);

            ed.WriteMessage("\nPoint ({0},{1},{2}) has been corrected to ({3},{4},{5}).", p.X, p.Y, p.Z, c.X, c.Y, c.Z);

            return c;
        }
    }    
}