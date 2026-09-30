import { z } from 'zod';

export const dashboardSearchSchema = z.object({
  days: z.literal([7, 14, 30]).optional().catch(undefined),
  subjectId: z.uuid().optional().catch(undefined),
});

export type DashboardSearch = z.infer<typeof dashboardSearchSchema>;
