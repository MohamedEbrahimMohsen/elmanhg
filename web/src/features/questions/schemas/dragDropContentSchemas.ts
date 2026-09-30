import { z } from 'zod';
import { diagramKeySchema } from './studentDiagramSchema';

export const dragDropBodySchema = z.object({
  image: z.object({
    key: z.string(),
    url: z.string().optional(),
    width: z.number(),
    height: z.number(),
    alt: z.string(),
  }),
  zones: z.array(
    z.object({
      id: z.string(),
      x: z.number(),
      y: z.number(),
      width: z.number(),
      height: z.number(),
      capacity: z.number(),
    }),
  ),
  items: z.array(z.object({ id: z.string(), text: z.string() })),
});

export const dragDropSpecSchema = diagramKeySchema;
