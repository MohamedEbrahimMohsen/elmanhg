import { z } from 'zod';

export const studentDiagramBodySchema = z.object({
  image: z.object({
    url: z.string().min(1),
    width: z.number().positive(),
    height: z.number().positive(),
    alt: z.string(),
  }),
  zones: z.array(
    z.object({
      id: z.string(),
      x: z.number(),
      y: z.number(),
      width: z.number(),
      height: z.number(),
      capacity: z.number().int().min(1),
    }),
  ),
  items: z.array(z.object({ id: z.string(), text: z.string() })),
});

export const diagramKeySchema = z.object({
  zones: z.array(z.object({ zoneId: z.string(), itemIds: z.array(z.string()), ordered: z.boolean() })),
});

export type StudentDiagram = z.infer<typeof studentDiagramBodySchema>;

export type DiagramKey = z.infer<typeof diagramKeySchema>['zones'];
