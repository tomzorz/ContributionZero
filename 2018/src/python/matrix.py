import sys
import random
import resource
from multiprocessing import Pool, cpu_count

#resource.setrlimit(resource.RLIMIT_STACK, (2**29,-1))
sys.setrecursionlimit(10**8)
resource.setrlimit(resource.RLIMIT_STACK, (resource.RLIM_INFINITY, resource.RLIM_INFINITY))

def prob(p):
	if random.uniform(0,1) < p:
		return 1
	return 0

def print_matrix(m):
	for i in m:
		print(i)

# a better, iterative floodfill implementation
def floodfill2(matrix, x, y):
	stack = []

	size = len(matrix[0])
	stack.append((x,y))

	while len(stack) > 0:
		x,y = stack.pop()
		if x >= size+2 or x < 0 or y >= size or y < 0: continue
		if matrix[x][y] != 1: continue
		matrix[x][y] = 2
		stack.append((x - 1, y))
		stack.append((x + 1, y))
		stack.append((x, y - 1))
		stack.append((x, y + 1))

# check if we can fill the path to the "bottom" of the matrix
def floodfill(matrix, x, y):
	if matrix[x][y] == 1:  
		matrix[x][y] = 2 
		
		# recursively invoke flood fill on all surrounding cells:
		if x > 0:
			floodfill(matrix,x-1,y)
		if x < len(matrix) - 1:
			floodfill(matrix,x+1,y)
		if y > 0:
			floodfill(matrix,x,y-1)
		if y < len(matrix[x]) - 1:
			#print(x,y)
			floodfill(matrix,x,y+1)

def run_test(p):
	# generate a test matrix
	m = []
	for i in range(n+2):
		m.append([prob(p) for i in range(n)])

	# add first and last line for fill help
	m[0] = [1 for _ in range(n)]
	m[n+1] = [1 for _ in range(n)]

	#print_matrix(m)
	floodfill2(m, 0, 0)
	#print_matrix(m)

	# check if it is fillable
	if m[n+1][0] == 2:
		return 1
	else:
		return 0


"""
Example usage:

> python3 matrix.py 0.592716 100 1000
Number of tests: 1000, Fillable: 490, Ration: 0.49
"""
if __name__ == '__main__':
	if len(sys.argv) < 4:
		print("specify p and n and t")
		sys.exit(1)

	p = float(sys.argv[1]) # probability
	n = int(sys.argv[2]) # matrix size
	t = int(sys.argv[3]) # trial number

	fillable = 0
	with Pool(cpu_count()) as pool:
		results = pool.map(run_test, [p]*t)

	#print(results)
	fillable = sum(results)
	print("Number of tests: {}, Fillable: {}, Ration: {}".format(t, fillable, fillable/t))