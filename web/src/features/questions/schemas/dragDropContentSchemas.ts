import { z } from 'zod';

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

export const dragDropSpecSchema = z.object({
  zones: z.array(z.object({ zoneId: z.string(), itemIds: z.array(z.string()), ordered: z.boolean() })),
});
